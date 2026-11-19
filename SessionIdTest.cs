using System;
using QuickFix;

public class SessionIdTest
{
    public static void Run()
    {
        var sessionId = new SessionID(
            "FIXT.1.1",
            "TEST-SENDER",
            "TEST-TARGET",
            "TEST-QUOTE-LATE-REF-SESSION-002");

        Console.WriteLine("Generated SessionID:");
        Console.WriteLine(sessionId.ToString());
    }
}