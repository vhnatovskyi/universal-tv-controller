using AVFoundation;
using Foundation;
using Speech;

namespace BraviaRemote;

internal static class VoiceInput
{
    public static async Task<string?> RecognizeAsync(string language, CancellationToken token)
    {
        var authorization = new TaskCompletionSource<SFSpeechRecognizerAuthorizationStatus>();
        SFSpeechRecognizer.RequestAuthorization(status => authorization.TrySetResult(status));
        if (await authorization.Task.WaitAsync(token) != SFSpeechRecognizerAuthorizationStatus.Authorized)
            throw new UnauthorizedAccessException("Speech recognition permission is required.");
        var microphone = new TaskCompletionSource<bool>();
        var session = AVAudioSession.SharedInstance();
        session.RequestRecordPermission(allowed => microphone.TrySetResult(allowed));
        if (!await microphone.Task.WaitAsync(token))
            throw new UnauthorizedAccessException("Microphone permission is required.");

        using var recognizer = new SFSpeechRecognizer(NSLocale.FromLocaleIdentifier(language == "uk" ? "uk-UA" : "en-US"));
        if (!recognizer.Available)
            throw new InvalidOperationException("Speech recognition is unavailable.");
        using var request = new SFSpeechAudioBufferRecognitionRequest { ShouldReportPartialResults = true };
        using var engine = new AVAudioEngine();
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcript = "";
        var silence = DateTimeOffset.UtcNow;
        using var recognition = recognizer.GetRecognitionTask(request, (recognized, error) =>
        {
            if (recognized is not null)
            {
                transcript = recognized.BestTranscription.FormattedString;
                silence = DateTimeOffset.UtcNow;
                if (recognized.Final)
                    result.TrySetResult(transcript);
            }
            if (error is not null)
                result.TrySetException(new InvalidOperationException(error.LocalizedDescription));
        });
        session.SetCategory(AVAudioSessionCategory.Record, AVAudioSessionCategoryOptions.DuckOthers);
        session.SetActive(true);
        var input = engine.InputNode;
        input.InstallTapOnBus(0, 1024, input.GetBusOutputFormat(0), (buffer, _) => request.Append(buffer));
        try
        {
            engine.Prepare();
            if (!engine.StartAndReturnError(out var error))
                throw new InvalidOperationException(error?.LocalizedDescription ?? "Could not start microphone.");
            while (!result.Task.IsCompleted)
            {
                await Task.Delay(200, token);
                if (transcript.Length > 0 && DateTimeOffset.UtcNow - silence > TimeSpan.FromSeconds(1.2))
                    return transcript;
            }
            return await result.Task;
        }
        finally
        {
            engine.Stop();
            input.RemoveTapOnBus(0);
            request.EndAudio();
            recognition.Cancel();
            session.SetActive(false);
        }
    }
}
