using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

namespace evergreennuget
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: evergreennuget <username> <password> <account> <rootfolder>");
                return 1;
            }

            string username = args[0];
            string password = args[1];
            string account = args[2];
            string rootfolder = args[3];

            CloneAllRepos(username, password, account, rootfolder);

            return 0;
        }

        static void CloneAllRepos(string username, string password, string account, string rootfolder)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            var url = $"https://api.bitbucket.org/2.0/repositories/{account}";
            dynamic repos;

            List<string> repourls = new List<string>();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Console.Write($"Retrieving repos");
                do
                {
                    var result = client.GetStringAsync(url).Result;

                    repos = JObject.Parse(result);

                    //Console.WriteLine($">>>{repos}<<<");
                    Console.Write(".");

                    foreach (var repo in repos.values)
                    {
                        JArray cloneurls = repo.links.clone;
                        string cloneurl = cloneurls
                            .Where(u => u["name"].ToString() == "https")
                            .Select(u => u["href"].ToString())
                            .Single();

                        repourls.Add(cloneurl);
                    }

                    url = repos.next;
                }
                while (!string.IsNullOrEmpty(url));
                Console.WriteLine();
            }

            Console.WriteLine($"Got {repourls.Count} repos.");


            string gitexe = @"C:\Program Files\Git\bin\git.exe";

            for (int retries = 0; Directory.Exists(rootfolder) && retries < 10; retries++)
            {
                try
                {
                    Console.WriteLine($"Deleting folder: '{rootfolder}'");
                    Process.Start("cmd.exe", $"/c rd /q /s \"{rootfolder}\"").WaitForExit();
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(2000);
                }
            }
            Console.WriteLine($"Creating folder: '{rootfolder}'");
            Directory.CreateDirectory(rootfolder);

            string dir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(rootfolder);

            repourls.Sort();

            for (int i = 0; i < repourls.Count; i++)
            {
                string repourl = repourls[i];
                url = repourl.Replace($"https://{username}@", $"https://{username}:{password}@");
                Console.WriteLine($"Cloning {i + 1}/{repourls.Count}: '{repourl}'");
                Process.Start(gitexe, $"--no-pager clone {url}").WaitForExit();

                string folder = repourl.Substring(repourl.LastIndexOf('/') + 1);
                if (folder.EndsWith(".git"))
                {
                    folder = folder.Substring(0, folder.Length - 4);
                }
                string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
                long size = files.Sum(f => new FileInfo(f).Length);
                long sizekb = size / 1024;
                Console.WriteLine($"Size: {sizekb} kb");
            }

            Directory.SetCurrentDirectory(dir);
        }
    }
}
