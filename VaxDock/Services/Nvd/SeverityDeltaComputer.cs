using System;

namespace VaxDrive.VaxDock.Services.Nvd;

public class SeverityDeltaComputer
{
    public bool IsAnomalousShift(double oldScore, double newScore)
    {
        return Math.Abs(newScore - oldScore) >= 3.0;
    }
}
