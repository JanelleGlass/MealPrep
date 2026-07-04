using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MealPrep.Services;

public class GitBackupService
{
    private readonly HttpClient _http;

    public GitBackupService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> CreateBackupPRAsync(string repo, string token, string basePath, Dictionary<string, string> files)
    {
        // 1. Get the default branch and its latest commit SHA
        var repoUrl = $"https://api.github.com/repos/{repo}";
        var repoRequest = new HttpRequestMessage(HttpMethod.Get, repoUrl);
        ConfigureRequest(repoRequest, token);
        var repoResponse = await _http.SendAsync(repoRequest);
        if (!repoResponse.IsSuccessStatusCode)
        {
            var error = await repoResponse.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API error ({repoResponse.StatusCode}): {error}");
        }
        var repoDoc = await JsonSerializer.DeserializeAsync<JsonElement>(
            await repoResponse.Content.ReadAsStreamAsync());
        var defaultBranch = repoDoc.GetProperty("default_branch").GetString()!;

        var refUrl = $"https://api.github.com/repos/{repo}/git/ref/heads/{defaultBranch}";
        var refRequest = new HttpRequestMessage(HttpMethod.Get, refUrl);
        ConfigureRequest(refRequest, token);
        var refResponse = await _http.SendAsync(refRequest);
        if (!refResponse.IsSuccessStatusCode)
        {
            var error = await refResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to get branch ref ({refResponse.StatusCode}): {error}");
        }
        var refDoc = await JsonSerializer.DeserializeAsync<JsonElement>(
            await refResponse.Content.ReadAsStreamAsync());
        var baseSha = refDoc.GetProperty("object").GetProperty("sha").GetString()!;

        // 2. Create a new branch
        var branchName = $"mealprep-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var createRefUrl = $"https://api.github.com/repos/{repo}/git/refs";
        var createRefBody = JsonSerializer.Serialize(new { @ref = $"refs/heads/{branchName}", sha = baseSha });
        var createRefRequest = new HttpRequestMessage(HttpMethod.Post, createRefUrl);
        ConfigureRequest(createRefRequest, token);
        createRefRequest.Content = new StringContent(createRefBody, Encoding.UTF8, "application/json");
        var createRefResponse = await _http.SendAsync(createRefRequest);
        if (!createRefResponse.IsSuccessStatusCode)
        {
            var error = await createRefResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create branch ({createRefResponse.StatusCode}): {error}");
        }

        // 3. Create/update each file on the new branch
        // If basePath looks like a file (e.g. legacy "mealprep-backup.json"), use its parent directory
        if (basePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var lastSlash = basePath.LastIndexOf('/');
            basePath = lastSlash >= 0 ? basePath[..lastSlash] : "";
        }
        var folder = string.IsNullOrWhiteSpace(basePath) ? "" : basePath.TrimEnd('/') + "/";

        foreach (var (fileName, content) in files)
        {
            var filePath = $"{folder}{fileName}";
            var contentsUrl = $"https://api.github.com/repos/{repo}/contents/{filePath}";
            string? fileSha = null;

            var getFileRequest = new HttpRequestMessage(HttpMethod.Get, $"{contentsUrl}?ref={branchName}");
            ConfigureRequest(getFileRequest, token);
            var getFileResponse = await _http.SendAsync(getFileRequest);
            if (getFileResponse.IsSuccessStatusCode)
            {
                var existing = await JsonSerializer.DeserializeAsync<JsonElement>(
                    await getFileResponse.Content.ReadAsStreamAsync());
                fileSha = existing.GetProperty("sha").GetString();
            }

            var fileBody = new Dictionary<string, string>
            {
                ["message"] = $"Update {fileName} - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
                ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                ["branch"] = branchName
            };
            if (fileSha != null)
                fileBody["sha"] = fileSha;

            var putRequest = new HttpRequestMessage(HttpMethod.Put, contentsUrl);
            ConfigureRequest(putRequest, token);
            putRequest.Content = new StringContent(JsonSerializer.Serialize(fileBody), Encoding.UTF8, "application/json");
            var putResponse = await _http.SendAsync(putRequest);
            if (!putResponse.IsSuccessStatusCode)
            {
                var error = await putResponse.Content.ReadAsStringAsync();
                throw new Exception($"Failed to create/update {fileName} ({putResponse.StatusCode}): {error}");
            }
        }

        // 4. Create the pull request
        var prUrl = $"https://api.github.com/repos/{repo}/pulls";
        var prBody = JsonSerializer.Serialize(new
        {
            title = $"MealPrep backup {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
            head = branchName,
            @base = defaultBranch,
            body = $"Automated MealPrep data backup.\n\nFiles updated:\n{string.Join("\n", files.Keys.Select(f => $"- {f}"))}"
        });
        var prRequest = new HttpRequestMessage(HttpMethod.Post, prUrl);
        ConfigureRequest(prRequest, token);
        prRequest.Content = new StringContent(prBody, Encoding.UTF8, "application/json");
        var prResponse = await _http.SendAsync(prRequest);
        if (!prResponse.IsSuccessStatusCode)
        {
            var error = await prResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create PR ({prResponse.StatusCode}): {error}");
        }

        var prDoc = await JsonSerializer.DeserializeAsync<JsonElement>(
            await prResponse.Content.ReadAsStreamAsync());
        return prDoc.GetProperty("html_url").GetString()!;
    }

    public async Task<Dictionary<string, string>> PullBackupFilesAsync(string repo, string token, string basePath)
    {
        if (basePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var lastSlash = basePath.LastIndexOf('/');
            basePath = lastSlash >= 0 ? basePath[..lastSlash] : "";
        }
        var folder = string.IsNullOrWhiteSpace(basePath) ? "" : basePath.TrimEnd('/') + "/";
        var fileNames = new[] { "ingredients.json", "meals.json", "pantry.json", "recipes-and-books.json" };
        var result = new Dictionary<string, string>();

        foreach (var fileName in fileNames)
        {
            var url = $"https://api.github.com/repos/{repo}/contents/{folder}{fileName}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            ConfigureRequest(request, token);
            var response = await _http.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                continue;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to pull {fileName} ({response.StatusCode}): {error}");
            }

            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync());
            var base64 = doc.GetProperty("content").GetString()!;
            var cleaned = base64.Replace("\n", "");
            result[fileName] = Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
        }

        return result;
    }

    // Legacy single-file pull for backward compatibility
    public async Task<string> PullBackupAsync(string repo, string token, string path)
    {
        var url = $"https://api.github.com/repos/{repo}/contents/{path}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        ConfigureRequest(request, token);
        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API error ({response.StatusCode}): {error}");
        }

        var doc = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync());
        var base64 = doc.GetProperty("content").GetString()!;
        var cleaned = base64.Replace("\n", "");
        return Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
    }

    private static void ConfigureRequest(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MealPrep", "1.0"));
    }
}
