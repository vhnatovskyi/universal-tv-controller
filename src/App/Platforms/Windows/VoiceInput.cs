using System.Speech.Recognition;

namespace UniversalTvRemote;

internal static class VoiceInput
{
    public static async Task<string?> RecognizeAsync(string language, CancellationToken token)
    {
        var installed = SpeechRecognitionEngine.InstalledRecognizers();
        var recognizer = installed.FirstOrDefault(item => item.Culture.TwoLetterISOLanguageName == language);
        if (recognizer is null)
            throw new InvalidOperationException("No Windows speech recognition language is installed.");

        using var engine = new SpeechRecognitionEngine(recognizer);
        engine.LoadGrammar(new DictationGrammar());
        engine.InitialSilenceTimeout = TimeSpan.FromSeconds(8);
        engine.BabbleTimeout = TimeSpan.FromSeconds(15);
        engine.EndSilenceTimeout = TimeSpan.FromMilliseconds(750);
        engine.SetInputToDefaultAudioDevice();
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.RecognizeCompleted += (_, args) =>
        {
            if (args.Error is { } error)
                result.TrySetException(error);
            else
                result.TrySetResult(args.Result?.Text);
        };
        engine.RecognizeAsync(RecognizeMode.Single);
        using var cancellation = token.Register(() =>
        {
            engine.RecognizeAsyncCancel();
            result.TrySetCanceled(token);
        });
        return await result.Task;
    }
}
