namespace AltCover.Recorder
{
#if DEBUG  // conditionally internal for Gendarme

    public
#else
    internal
#endif
        enum Sampling
    {
        All = 0,
        Single = 1
    }
}