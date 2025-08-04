using System;
using System.Text.RegularExpressions;

class TestRollDetection
{
    static void Main(string[] args)
    {
        // Test the exact message format you described
        string testMessage = "Random! You roll a 666.";
        
        Console.WriteLine($"Testing message: '{testMessage}'");
        Console.WriteLine();
        
        // Test debug pattern
        var debugMatch = Regex.Match(testMessage, @"Random! (.+) rolls? a (\d+) \(out of \d+\)\.");
        Console.WriteLine($"Debug pattern match: {debugMatch.Success}");
        if (debugMatch.Success)
        {
            Console.WriteLine($"  Player: '{debugMatch.Groups[1].Value}'");
            Console.WriteLine($"  Roll: {debugMatch.Groups[2].Value}");
        }
        
        // Test normal pattern
        var normalMatch = Regex.Match(testMessage, @"Random! (.+) rolls? a (\d+)\.");
        Console.WriteLine($"Normal pattern match: {normalMatch.Success}");
        if (normalMatch.Success)
        {
            Console.WriteLine($"  Player: '{normalMatch.Groups[1].Value}'");
            Console.WriteLine($"  Roll: {normalMatch.Groups[2].Value}");
        }
        
        // Test other variations
        string[] testMessages = {
            "Random! You roll a 666.",
            "Random! Someone rolls a 666.",
            "Random! Player Name rolls a 666.",
            "Random! Test UserJenova rolls a 666."
        };
        
        Console.WriteLine("\nTesting variations:");
        foreach (var msg in testMessages)
        {
            var match = Regex.Match(msg, @"Random! (.+) rolls? a (\d+)\.");
            Console.WriteLine($"'{msg}' -> Match: {match.Success}");
            if (match.Success)
            {
                Console.WriteLine($"  Player: '{match.Groups[1].Value}', Roll: {match.Groups[2].Value}");
            }
        }
    }
}