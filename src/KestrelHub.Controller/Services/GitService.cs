using LibGit2Sharp;

namespace KestrelHub.Controller.Services;

public interface IGitService
{
    Task CloneAsync(string gitUrl, string branch, string targetPath);
    Task CleanupAsync(string path);
}

public class GitService : IGitService
{
    public Task CloneAsync(string gitUrl, string branch, string targetPath)
    {
        return Task.Run(() =>
        {
            var options = new CloneOptions
            {
                BranchName = branch,
                Checkout = true
            };
            Repository.Clone(gitUrl, targetPath, options);
        });
    }

    public Task CleanupAsync(string path)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(path))
                return;

            Directory.Delete(path, recursive: true);
        });
    }
}
