using System;
using System.Security.Cryptography;
using System.Text;
using FluffyByte.Tools;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// GateKeeper handles authentication of vessels after TCP/UDP handshake completes.
/// Uses challenge-response authentication to verify clients before allowing game access.
/// </summary>
public static class GateKeeper
{
    private const int CHALLENGE_TIMEOUT_SECONDS = 30;

    // Server secret - in production, load from config or environment variable
    private const string SERVER_SECRET = "FluffyByte_OPUL_SecretKey_2025";
    
    /// <summary>
    /// Initiates the authentication challenge for a newly connected vessel.
    /// </summary>
    /// <param name="vessel">The vessel to authenticate</param>
    public static async Task<bool> AuthenticateVesselAsync(Vessel vessel)
    {
        try
        {
            Scribe.Info($"[GateKeeper] Starting authentication for Vessel {vessel.Id}");

            // Generate challenge
            string challenge = GenerateChallenge();
            string expectedResponse = ComputeExpectedResponse(challenge);

            // Send challenge via TCP (binary)
            await SendChallengeAsync(vessel, challenge);

            // Wait for response with timeout
            var responseTask = WaitForResponseAsync(vessel);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(CHALLENGE_TIMEOUT_SECONDS));
            var completedTask = await Task.WhenAny(responseTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Scribe.Warn($"[GateKeeper] Vessel {vessel.Id} authentication timed out.");
                await vessel.DisconnectAsync();
                return false;
            }

            string clientResponse = await responseTask;

            // Verify response
            if (VerifyResponse(expectedResponse, clientResponse))
            {
                vessel.IsAuthenticated = true;
                Scribe.Info($"[GateKeeper] Vessel {vessel.Id} authenticated successfully!");

                // Send success message
                await vessel.TcpIO.WriteTextAsync("AUTH_SUCCESS");

                return true;
            }
            else
            {
                Scribe.Warn($"[GateKeeper] Vessel {vessel.Id} failed authentication (invalid response).");
                await vessel.TcpIO.WriteTextAsync("AUTH_FAILED");
                await vessel.DisconnectAsync();
                return false;
            }
        }
        catch (IOException)
        {
            Scribe.Warn($"[GateKeeper] Vessel {vessel.Id} disconnected during authentication.");
            
            await vessel.DisconnectAsync();
            
            return false;
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
            await vessel.DisconnectAsync();
            return false;
        }
    }

    /// <summary>
    /// Generates a random challenge string for the client to respond to.
    /// </summary>
    private static string GenerateChallenge()
    {
        // Create a random challenge (timestamp + random bytes)
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] randomBytes = new byte[16];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        string challenge = $"{timestamp}:{Convert.ToBase64String(randomBytes)}";
        return challenge;
    }

    /// <summary>
    /// Computes the expected response using HMACSHA256.
    /// </summary>
    private static string ComputeExpectedResponse(string challenge)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SERVER_SECRET));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(challenge));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Sends the authentication challenge to the vessel via TCP text.
    /// </summary>
    private async static Task SendChallengeAsync(Vessel vessel, string challenge)
    {
        // Format: "AUTH_CHALLENGE|<challenge>"
        string message = $"AUTH_CHALLENGE|{challenge}";

        await vessel.TcpIO.WriteTextAsync(message);

        Scribe.Debug($"[GateKeeper] Sent challenge to Vessel {vessel.Id}: {challenge}");
    }

    /// <summary>
    /// Waits for the vessel to send its authentication response.
    /// </summary>
    private async static Task<string> WaitForResponseAsync(Vessel vessel)
    {
        // Read text response from client (format: "AUTH_RESPONSE|<hash>")
        string message = await vessel.TcpIO.ReadTextAsync();

        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        // Parse response
        if (message.StartsWith("AUTH_RESPONSE|"))
        {
            string response = message.Split('|')[1];
            Scribe.Debug($"[GateKeeper] Received response from Vessel {vessel.Id}");
            return response;
        }

        return string.Empty;
    }

    /// <summary>
    /// Verifies that the client's response matches the expected value.
    /// </summary>
    private static bool VerifyResponse(string expected, string actual)
    {
        if (string.IsNullOrEmpty(actual))
            return false;

        // Use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual)
        );
    }
}