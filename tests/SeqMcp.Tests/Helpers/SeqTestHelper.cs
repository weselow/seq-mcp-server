using System.Net;

namespace SeqMcp.Tests.Helpers;

/// <summary>
/// Helper class for Seq integration tests
/// </summary>
public static class SeqTestHelper
{
    public const string DefaultSeqUrl = "http://localhost:5341";

    /// <summary>
    /// Checks if Seq server is available at the default URL
    /// </summary>
    public static async Task<bool> IsSeqAvailable(string seqUrl = DefaultSeqUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await client.GetAsync($"{seqUrl}/api");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the Seq URL from environment variable or uses default
    /// </summary>
    public static string GetSeqUrl()
    {
        return Environment.GetEnvironmentVariable("SEQ_URL")
               ?? Environment.GetEnvironmentVariable("SEQ_SERVER_URL")
               ?? DefaultSeqUrl;
    }

    /// <summary>
    /// Gets the Seq API key from environment variable if set
    /// </summary>
    public static string? GetSeqApiKey()
    {
        return Environment.GetEnvironmentVariable("SEQ_API_KEY");
    }

    /// <summary>
    /// Skips test if Seq server is not available
    /// </summary>
    public static async Task<bool> ShouldSkipIntegrationTest()
    {
        var seqUrl = GetSeqUrl();
        var isAvailable = await IsSeqAvailable(seqUrl);

        if (!isAvailable)
        {
            Console.WriteLine($"⚠️  Skipping integration test: Seq server not available at {seqUrl}");
            Console.WriteLine($"💡 To run integration tests:");
            Console.WriteLine($"   1. Start Seq: docker run -d --name seq -e ACCEPT_EULA=Y -p 5341:80 -p 5342:5341 datalust/seq");
            Console.WriteLine($"   2. Run tests: dotnet test");
        }

        return !isAvailable;
    }
}
