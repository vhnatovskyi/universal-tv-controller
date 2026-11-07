using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using UniversalTvRemote;
using static UniversalTvRemote.GoogleTvWire;

var wake = GoogleTvApps.CreateWakePacket("02-11-22-33-44-55");
var wakeMac = Convert.FromHexString("021122334455");
if (wake.Length != 102 || !wake.Take(6).All(value => value == 255)
    || Enumerable.Range(0, 16).Any(index => !wake.AsSpan(6 + index * 6, 6).SequenceEqual(wakeMac)))
    throw new Exception("Invalid Wake-on-LAN packet");
foreach (var invalid in new[] { "", "invalid", "00:00:00:00:00:00", "FF:FF:FF:FF:FF:FF", "02:11:22:33:44" })
{
    try { GoogleTvApps.CreateWakePacket(invalid); throw new Exception("Invalid wake MAC was accepted"); }
    catch (ArgumentException) { }
}

using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=Protocol test",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
using var serverCertificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12,""),"");
var pairing = new GoogleTvPairing(Convert.ToHexString(serverCertificate.GetCertHash(HashAlgorithmName.SHA256)),
    Convert.ToBase64String(generated.Export(X509ContentType.Pkcs12,"")));
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var token=timeout.Token;
var listener=new TcpListener(IPAddress.Loopback,6466);
listener.Start();
var volumeCases = new (int Initial, int Target)[] { (10, 3), (0, 5), (100, 10), (5, 30), (0, 100), (3, 3), (100, 3) };
using var holdCancellation = new CancellationTokenSource();
using var settingsCancellation = new CancellationTokenSource();
try
{
    var server=Task.Run(async () =>
    {
        await ServeAsync(stream => ExpectSettingsAsync(stream));
        await ServeAsync(stream => ExpectSettingsAsync(stream, settingsCancellation));
        await ServeAsync(stream => ExpectKeyAsync(stream, 26, reportedPower: false), screenOn: true, features: 51);
        await ServeAsync(stream => ExpectKeyAsync(stream, 26, reportedPower: true), screenOn: false, features: 51);
        await ServeAsync(ExpectNoKeyAsync, screenOn: true, features: 51);
        await ServeAsync(ExpectNoKeyAsync, screenOn: false, features: 51);
        await ServeAsync(stream => ExpectKeyAsync(stream, 26, reportedPower: false), features: 51);
        await ServeAsync(async stream => { await Task.Delay(50,token); });
        await ServeAsync(async stream =>
        {
            var command=ReadFields(await ReceiveAsync(stream,token));
            var key=ReadFields(command.Single(f=>f.Number==10).Data!).Single(f=>f.Number==1).Value;
            if(key!=24)throw new Exception("Wrong volume key");
        });
        await ServeAsync(async stream =>
        {
            var command=ReadFields(await ReceiveAsync(stream,token));
            if(command.Single().Number!=90)throw new Exception("Missing app command");
            var launch = ReadFields(command.Single().Data!);
            var uri = System.Text.Encoding.UTF8.GetString(launch.Single(f => f.Number == 1).Data!);
            if (launch.Count != 1 || uri != "com.kyivstar.tv.androidtv://")
                throw new Exception("Incorrect app launch format");
            var app=Bytes(20,Bytes(1,Text(12,"com.kyivstar.tv.androidtv")));
            // Deliberately split the wire frame to verify stream framing.
            var frame=Message(new byte[]{(byte)app.Length},app);
            await stream.WriteAsync(frame.AsMemory(0,2),token);
            await Task.Delay(20,token);
            await stream.WriteAsync(frame.AsMemory(2),token);
            await Task.Delay(80,token);
        }, initialPackage: "com.kyivstar.tv.androidtv");
        await ServeAsync(async stream =>
        {
            var command = ReadFields(await ReceiveAsync(stream, token));
            var launch = ReadFields(command.Single(field => field.Number == 90).Data!);
            var uri = System.Text.Encoding.UTF8.GetString(launch.Single(field => field.Number == 1).Data!);
            if (launch.Count != 1 || uri != "nflx://www.netflix.com/browse")
                throw new Exception("Netflix must use its native app link");
            await SendAsync(stream, Bytes(20, Bytes(1, Text(12, "com.netflix.ninja"))), token);
        });
        await ServeAsync(async stream =>
        {
            var command=ReadFields(await ReceiveAsync(stream,token));
            var batch=ReadFields(command.Single(f=>f.Number==21).Data!);
            var edit=ReadFields(batch.Single(f=>f.Number==3).Data!);
            var value=ReadFields(edit.Single(f=>f.Number==2).Data!).Single(f=>f.Number==3).Data!;
            if(System.Text.Encoding.UTF8.GetString(value)!="voice test")
                throw new Exception("Incorrect dictated text");
        }, textField: true);
        foreach (var (initial, target) in volumeCases)
            await ServeAsync(stream => ServeVolumeAsync(stream, initial, target), initialVolume: initial);
        await ServeAsync(stream => ServeVolumeAsync(stream, 5, 8), initialVolume: 5);
        await ServeAsync(async stream =>
        {
            await ExpectKeyAsync(stream, 24);
            // Leave the volume unchanged: the client must fail, not report the preset as set.
            await ExpectNoKeyAsync(stream);
        }, initialVolume: 5);
        await ServeAsync(async stream =>
        {
            await ExpectKeyAsync(stream, 26);
            await ExpectNoKeyAsync(stream);
        }, screenOn: false);
    },token);
    await GoogleTvApps.OpenSettingsAsync(IPAddress.Loopback, pairing);
    try
    {
        await GoogleTvApps.OpenSettingsAsync(IPAddress.Loopback, pairing, settingsCancellation.Token);
        throw new Exception("Settings hold ignored cancellation");
    }
    catch (OperationCanceledException) when (settingsCancellation.IsCancellationRequested) { }
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: false);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: true);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: true);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: false);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, sonyKey: 0);
    if(await GoogleTvApps.ControlAsync(IPAddress.Loopback,pairing)!=13)throw new Exception("Missing volume");
    await GoogleTvApps.ControlAsync(IPAddress.Loopback,pairing,sonyKey:30);
    await GoogleTvApps.LaunchAsync(IPAddress.Loopback,pairing,"com.kyivstar.tv.androidtv");
    await GoogleTvApps.LaunchAsync(IPAddress.Loopback,pairing,"com.netflix.ninja");
    await GoogleTvApps.TypeTextAsync(IPAddress.Loopback,pairing,"voice test");
    foreach (var (initial, target) in volumeCases)
    {
        var last = -1;
        await GoogleTvApps.SetVolumeAsync(IPAddress.Loopback, pairing, target, value => last = value);
        if (last != target)
            throw new Exception($"Volume {initial} -> {target} was not confirmed");
    }
    try
    {
        await GoogleTvApps.HoldVolumeAsync(IPAddress.Loopback, pairing, 1, value =>
        {
            if (value == 8) holdCancellation.Cancel();
        }, holdCancellation.Token);
        throw new Exception("Holding volume did not stop on cancellation");
    }
    catch (OperationCanceledException) when (holdCancellation.IsCancellationRequested) { }
    try
    {
        await GoogleTvApps.SetVolumeAsync(IPAddress.Loopback, pairing, 10);
        throw new Exception("Unconfirmed volume was accepted");
    }
    catch (OperationCanceledException) { }
    try
    {
        await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: true);
        throw new Exception("Unconfirmed wake was accepted");
    }
    catch (TvPowerUnconfirmedException error) when (error.ExpectedOn == true) { }
    await server;
    Console.WriteLine("Settings HOME hold and release after cancellation, Sony feature 16, power confirmation, app launch, volume presets 3/5/10/30/100 from different levels, hold cancellation, missing volume confirmation and text input passed.");
}
finally { listener.Stop(); }

async Task ServeAsync(Func<SslStream,Task> action, bool textField = false, bool screenOn = true, uint features = 639,
    string? initialPackage = null, int initialVolume = 13)
{
    using var client=await listener.AcceptTcpClientAsync(token);
    using var stream=new SslStream(client.GetStream(),false,(_,_,_,_)=>true);
    try { await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
    {
        ServerCertificate=serverCertificate,ClientCertificateRequired=true
    },token); } catch(Exception error) { Console.WriteLine(error); throw; }
    await SendAsync(stream,Bytes(1,Number(1,features)),token);
    var config=ReadFields(await ReceiveAsync(stream,token));
    if(config.Single().Number!=1)throw new Exception("Missing configure");
    var negotiated = ReadFields(config.Single().Data!).Single(f => f.Number == 1).Value;
    if ((features & 16) != 0 && (negotiated & 16) == 0)
        throw new Exception("Sony system-control feature 16 was dropped during negotiation");
    await SendAsync(stream,Bytes(2,[]),token);
    var active=ReadFields(await ReceiveAsync(stream,token));
    if(active.Single().Number!=2)throw new Exception("Missing active");
    if ((features & 64) != 0)
        await SendAsync(stream,Bytes(50,Message(Number(6,100),Number(7,(uint)initialVolume))),token);
    if (initialPackage is not null)
        await SendAsync(stream, Bytes(20, Bytes(1, Text(12, initialPackage))), token);
    if(textField)
        await SendAsync(stream,Bytes(20,Message(Bytes(1,Message(Number(1,7),Number(2,1))),
            Bytes(2,Message(Number(1,2),Number(3,0),Number(4,0))))),token);
    await SendAsync(stream,Bytes(40,Number(1,screenOn ? 1u : 0u)),token);
    await action(stream);
}

async Task ExpectKeyAsync(SslStream stream, uint expected, bool? reportedPower = null)
{
    var command = ReadFields(await ReceiveAsync(stream, token));
    var key = ReadFields(command.Single(f => f.Number == 10).Data!);
    if (key.Single(f => f.Number == 1).Value != expected || key.Single(f => f.Number == 2).Value != 3)
        throw new Exception($"Expected a short press of key {expected}");
    if (reportedPower is { } on)
        await SendAsync(stream, Bytes(40, Number(1, on ? 1u : 0u)), token);
}

async Task ExpectSettingsAsync(SslStream stream, CancellationTokenSource? cancel = null)
{
    var down = ReadFields(ReadFields(await ReceiveAsync(stream, token)).Single(field => field.Number == 10).Data!);
    if (down.Single(field => field.Number == 1).Value != 3 || down.Single(field => field.Number == 2).Value != 1)
        throw new Exception("Settings must press HOME down");
    var held = System.Diagnostics.Stopwatch.StartNew();
    cancel?.Cancel();
    var up = ReadFields(ReadFields(await ReceiveAsync(stream, token)).Single(field => field.Number == 10).Data!);
    if (up.Single(field => field.Number == 1).Value != 3 || up.Single(field => field.Number == 2).Value != 2)
        throw new Exception("Settings must release HOME even after cancellation");
    if (cancel is null && held.ElapsedMilliseconds < 1100)
        throw new Exception("Settings HOME hold is too short");
    await ExpectNoKeyAsync(stream);
}

async Task ExpectNoKeyAsync(SslStream stream)
{
    var probe = new byte[1];
    if (await stream.ReadAsync(probe, token) != 0)
        throw new Exception("TV already in requested state received an unwanted power toggle");
}

async Task ServeVolumeAsync(SslStream stream, int current, int target)
{
    while (current != target)
    {
        var direction = Math.Sign(target - current);
        await ExpectKeyAsync(stream, direction > 0 ? 24u : 25u);
        current += direction;
        await SendAsync(stream, Bytes(50, Message(Number(6,100), Number(7,(uint)current))), token);
    }
    await ExpectNoKeyAsync(stream);
}
