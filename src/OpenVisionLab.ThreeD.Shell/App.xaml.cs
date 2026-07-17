using System.Windows;

namespace OpenVisionLab.ThreeD.Shell;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        const string verificationOption = "--verify-calibration-viewmodel";
        var verificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(verificationOption, StringComparison.OrdinalIgnoreCase));
        if (verificationIndex >= 0)
        {
            if (verificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{verificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = CalibrationCenterViewModelVerification.Verify(
                e.Args[verificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        base.OnStartup(e);
    }
}
