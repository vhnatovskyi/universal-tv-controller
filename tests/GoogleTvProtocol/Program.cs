using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BraviaRemote;
using static BraviaRemote.GoogleTvWire;

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
try
{
    var server=Task.Run(async () =>
    {
        await ServeAsync(stream => ExpectKeyAsync(stream, 26), screenOn: true, features: 35);
        await ServeAsync(stream => ExpectKeyAsync(stream, 26), screenOn: false, features: 35);
        await ServeAsync(ExpectNoKeyAsync, screenOn: true, features: 35);
        await ServeAsync(ExpectNoKeyAsync, screenOn: false, features: 35);
        await ServeAsync(stream => ExpectKeyAsync(stream, 26), features: 35);
        await ServeAsync(async stream =>
        {
            var command = ReadFields(await ReceiveAsync(stream, token));
            var launch = ReadFields(command.Single(f => f.Number == 90).Data!);
            var uri = System.Text.Encoding.UTF8.GetString(launch.Single(f => f.Number == 1).Data!);
            if (uri != "market://launch?id=com.android.tv.settings")
                throw new Exception("Settings button did not launch TV settings");
            await SendAsync(stream, Bytes(20, Bytes(1, Text(12, "com.android.tv.settings"))), token);
            await Task.Delay(50, token);
        });
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
            var app=Bytes(20,Bytes(1,Text(12,"com.kyivstar.tv.androidtv")));
            // Deliberately split the wire frame to verify stream framing.
            var frame=Message(new byte[]{(byte)app.Length},app);
            await stream.WriteAsync(frame.AsMemory(0,2),token);
            await Task.Delay(20,token);
            await stream.WriteAsync(frame.AsMemory(2),token);
            await Task.Delay(80,token);
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
    },token);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: false);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: true);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: true);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, power: false);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, sonyKey: 0);
    await GoogleTvApps.ControlAsync(IPAddress.Loopback, pairing, sonyKey: 7);
    if(await GoogleTvApps.ControlAsync(IPAddress.Loopback,pairing)!=13)throw new Exception("Missing volume");
    await GoogleTvApps.ControlAsync(IPAddress.Loopback,pairing,sonyKey:30);
    await GoogleTvApps.LaunchAsync(IPAddress.Loopback,pairing,"com.kyivstar.tv.androidtv");
    await GoogleTvApps.TypeTextAsync(IPAddress.Loopback,pairing,"voice test");
    await server;
    Console.WriteLine("Power on/off/toggle, idempotence, optional volume, settings launch confirmation, volume, app launch and text input passed.");
}
finally { listener.Stop(); }

async Task ServeAsync(Func<SslStream,Task> action, bool textField = false, bool screenOn = true, uint features = 615)
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
    await SendAsync(stream,Bytes(2,[]),token);
    var active=ReadFields(await ReceiveAsync(stream,token));
    if(active.Single().Number!=2)throw new Exception("Missing active");
    if ((features & 64) != 0)
        await SendAsync(stream,Bytes(50,Number(7,13)),token);
    if(textField)
        await SendAsync(stream,Bytes(20,Message(Bytes(1,Message(Number(1,7),Number(2,1))),
            Bytes(2,Message(Number(1,2),Number(3,0),Number(4,0))))),token);
    await SendAsync(stream,Bytes(40,Number(1,screenOn ? 1u : 0u)),token);
    await action(stream);
}

async Task ExpectKeyAsync(SslStream stream, uint expected)
{
    var command = ReadFields(await ReceiveAsync(stream, token));
    var key = ReadFields(command.Single(f => f.Number == 10).Data!);
    if (key.Single(f => f.Number == 1).Value != expected || key.Single(f => f.Number == 2).Value != 3)
        throw new Exception($"Expected a short press of key {expected}");
}

async Task ExpectNoKeyAsync(SslStream stream)
{
    var probe = new byte[1];
    if (await stream.ReadAsync(probe, token) != 0)
        throw new Exception("TV already in requested state received an unwanted power toggle");
}
