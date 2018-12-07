namespace AltCover.Recorder
{
#if DEBUG  // conditionally internal for Gendarme

    public
#else
    internal
#endif
        enum ReportFormat
    {
        NCover = 0,
        OpenCover = 1,
        OpenCoverWithTracking = 2
    }
}