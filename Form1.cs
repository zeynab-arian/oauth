using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace OAuthLoginApp
{
    public partial class Form1 : Form
    {
        private const string CLIENT_ID = "A5pMusdv4RkYeCSEMeEs"; 
        private const string SCOPE = "basic"; 
        private const string AUTHORIZE_URL = "https://oauth.um.ac.ir/openapi-oauth/authorize";
        private const string TOKEN_URL = "https://oauth.um.ac.ir/openapi-oauth/token"; 
        private const int PORT = 3000; 

        public Form1()
        {
            InitializeComponent();
        }

        private async void buttonLogin_Click(object sender, EventArgs e)
        {
            var (codeVerifier, codeChallenge) = GeneratePkceCodes();
            var authorizationUrl = GetAuthorizationUrl(CLIENT_ID, "http://localhost:3000/callback", SCOPE, codeChallenge);

            // Start the local HTTP listener
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{PORT}/callback/");
            listener.Start();
            Console.WriteLine("Listening for authorization callback...");

            // Open the authorization URL in the default web browser
            OpenBrowser(authorizationUrl);

            // Wait for the authorization response
            var context = await listener.GetContextAsync();
            var request = context.Request;

            // Extract the authorization code from the query parameters
            var query = request.QueryString;
            string authorizationCode = query["code"];

            // Send a response back to the browser
            var response = context.Response;
            string responseString = "<html><body><h1>Authorization successful! You can close this window.</h1></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            await output.WriteAsync(buffer, 0, buffer.Length);
            response.Close();

            // Exchange the authorization code for an access token
            var tokenResponse = await ExchangeCodeForToken(authorizationCode, "http://localhost:3000/callback", codeVerifier);

            // Display the access token
            MessageBox.Show($"Access Token:\n{tokenResponse}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            listener.Stop();
        }
        private static (string codeVerifier, string codeChallenge) GeneratePkceCodes()
        {
            var randomGen = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            randomGen.GetBytes(bytes);
            
            var codeVerifier = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            
            using var sha256 = SHA256.Create();
            var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            var codeChallenge = Convert.ToBase64String(challengeBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            
            return (codeVerifier, codeChallenge);
        }

        private static string GetAuthorizationUrl(string clientId, string redirectUri, string scope, string codeChallenge)
        {
            return $"{AUTHORIZE_URL}?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(scope)}&code_challenge_method=S256&code_challenge={codeChallenge}";
        }

        private static async Task<string> ExchangeCodeForToken(string code, string redirectUri, string codeVerifier)
        {
            using var httpClient = new HttpClient();
            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri },
                { "client_id", CLIENT_ID },
                { "code_verifier", codeVerifier }
            };

            var response = await httpClient.PostAsync(TOKEN_URL, new FormUrlEncodedContent(parameters));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return ExtractAccessToken(content);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Error exchanging code for token. Status Code: {response.StatusCode}, Reason: {response.ReasonPhrase}, Error Details: {errorContent}";
                throw new Exception(errorMessage);
            }
        }

        private static string ExtractAccessToken(string responseBody)
        {
            var json = JObject.Parse(responseBody);
            return json["access_token"]?.ToString() ?? "No access token found.";
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true 
                });
                Console.WriteLine("Browser opened for authorization.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening browser: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
