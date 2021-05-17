using static System.Console;

#if DEBUG
    WriteLine("DEBUG");
#elif RELEASE
    WriteLine("RELEASE");
#else
    WriteLine("??");
#endif
