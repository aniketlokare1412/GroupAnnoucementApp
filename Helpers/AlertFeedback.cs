namespace GroupAnnouncementApp.Helpers;

// Sound and vibration for the panic button. No sound files: the phone's tone generator is used.
public static class AlertFeedback
{
    // Member, right after pressing the panic button: a buzzer tone and a vibration.
    public static void Buzz()
    {
        Vibrate(700);
        PlayTone(true, 900);
    }

    // Admin: one short beep when a panic count goes up.
    public static void Beep()
    {
        PlayTone(false, 300);
    }

    private static void Vibrate(int milliseconds)
    {
        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(milliseconds));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("VIBRATE ERROR: " + ex);
        }
    }

#if ANDROID
    private static void PlayTone(bool buzzer, int milliseconds)
    {
        Task.Run(async () =>
        {
            Android.Media.ToneGenerator? tone = null;
            try
            {
                tone = new Android.Media.ToneGenerator(Android.Media.Stream.Music, 100);
                tone.StartTone(buzzer ? Android.Media.Tone.CdmaAlertCallGuard : Android.Media.Tone.PropBeep, milliseconds);
                await Task.Delay(milliseconds + 150);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("TONE ERROR: " + ex);
            }
            finally
            {
                tone?.Release();
            }
        });
    }
#else
    private static void PlayTone(bool buzzer, int milliseconds)
    {
    }
#endif
}
