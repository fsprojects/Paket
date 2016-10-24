using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    public class ArgumentParserTests
    {
        class DummyFileSystemProxy : IFileSystemProxy
        {
            private readonly string assembly;

            public DummyFileSystemProxy(string assembly)
            {
                this.assembly = assembly;
            }

            public bool FileExists(string filename)
            {
                return false;
            }

            public void CopyFile(string fileFrom, string fileTo, bool overwrite = false)
            {
            }

            public void DeleteFile(string filename)
            {
                
            }

            public Stream CreateFile(string tmpFile)
            {
                throw new NotImplementedException();
            }

            public string GetLocalFileVersion(string filename)
            {
                return "";
            }

            public void MoveFile(string fromFile, string toFile)
            {
                
            }

            public void ExtractToDirectory(string zipFile, string targetLocation)
            {
                
            }

            public DateTime GetLastWriteTime(string filename)
            {
                return DateTime.Now;
            }

            public void Touch(string filename)
            {
                
            }

            public void CreateDirectory(string path)
            {
            }

            public IEnumerable<string> GetDirectories(string path)
            {
                return Enumerable.Empty<string>();
            }

            public bool DirectoryExists(string path)
            {
                return false;
            }

            public IEnumerable<string> EnumerateFiles(string path, string filter, SearchOption searchOption)
            {
                return Enumerable.Empty<string>();
            }

            public void DeleteDirectory(string path, bool recursive)
            {
                
            }

            public string GetExecutingAssemblyPath()
            {
                return assembly;
            }

            public string GetTempPath()
            {
                return "C:\\temp";
            }
        }

        private static readonly IFileSystemProxy NormalModeFileSystemSystem = new DummyFileSystemProxy("C:\\repo\\.paket\\paket.bootstrapper.exe");
        private static readonly IFileSystemProxy MagicModeFileSystemSystem = new DummyFileSystemProxy("C:\\repo\\paket.exe");

        private static BootstrapperOptions Parse(IEnumerable<string> arguments, NameValueCollection appSettings, IDictionary envVariables, IEnumerable<string> argumentsInDependenciesFile = null)
        {
            return ArgumentParser.ParseArgumentsAndConfigurations(arguments, appSettings, envVariables, NormalModeFileSystemSystem, argumentsInDependenciesFile);
        }

        private static BootstrapperOptions ParseMagic(IEnumerable<string> arguments, NameValueCollection appSettings, IDictionary envVariables, IEnumerable<string> argumentsInDependenciesFile = null)
        {
            return ArgumentParser.ParseArgumentsAndConfigurations(arguments, appSettings, envVariables, MagicModeFileSystemSystem, argumentsInDependenciesFile);
        }

        [Test]
        public void NullArguments_GetDefault()
        {
            //arrange
            //act
            //assert
            Assert.Throws<ArgumentNullException>(() => Parse(null, null, null));
        }

        [Test]
        public void EmptyArguments_GetDefault()
        {
            //arrange

            //act
            var result = Parse(new string[] { }, null, null);

            //assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ForceNuget, Is.False);
            Assert.That(result.PreferNuget, Is.False);
            Assert.That(result.Silent, Is.False);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.DownloadArguments, Is.Not.Null);
            Assert.That(result.DownloadArguments.DoSelfUpdate, Is.False);
            Assert.That(result.DownloadArguments.Folder, Is.Not.Null);
            Assert.That(result.DownloadArguments.IgnoreCache, Is.False);
            Assert.That(result.DownloadArguments.IgnorePrerelease, Is.True);
            Assert.That(result.DownloadArguments.LatestVersion, Is.Empty);
            Assert.That(result.DownloadArguments.NugetSource, Is.Null);
            Assert.That(result.DownloadArguments.Target, Does.EndWith("paket.exe"));
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.Null);
            Assert.That(result.ShowHelp, Is.False);
            Assert.That(result.Run, Is.False);
            Assert.That(result.RunArgs, Is.Empty);

            var knownProps = new[] { "DownloadArguments.MaxFileAgeInMinutes", "DownloadArguments.Folder", "DownloadArguments.Target", "DownloadArguments.NugetSource", "DownloadArguments.DoSelfUpdate", "DownloadArguments.LatestVersion", "DownloadArguments.IgnorePrerelease", "DownloadArguments.IgnoreCache", "Silent", "ForceNuget", "PreferNuget", "UnprocessedCommandArgs", "ShowHelp", "Run", "RunArgs" };
            var allProperties = GetAllProperties(result);
            Assert.That(allProperties, Is.Not.Null.And.Count.EqualTo(knownProps.Length));
            Assert.That(allProperties, Is.EquivalentTo(knownProps));
        }

        private List<string> GetAllProperties(object valueFrom, string prefix = null)
        {
            var allProps = new List<string>();
            valueFrom.GetType().GetProperties().ToList().ForEach(prop =>
            {
                var valueResult = prop.GetValue(valueFrom);
                var propName = prop.Name;
                if (!String.IsNullOrEmpty(prefix))
                    propName = String.Format("{0}.{1}", prefix, propName);
                if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string) && !prop.PropertyType.IsGenericType)
                    allProps.AddRange(GetAllProperties(valueResult, propName));
                else
                    allProps.Add(propName);
            });
            return allProps;
        }

        [Test]
        public void Silent()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.Silent }, null, null);

            //assert
            Assert.That(result.Silent, Is.True);
        }

        [Test]
        public void ForceNuget()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.ForceNuget }, null, null);

            //assert
            Assert.That(result.ForceNuget, Is.True);
        }

        [Test]
        public void ForceNuget_FromAppSettings()
        {
            //arrange
            var appSettings = new NameValueCollection();
            appSettings.Add(ArgumentParser.AppSettingKeys.ForceNuget, "TrUe");

            //act
            var result = Parse(new string[] { }, appSettings, null);

            //assert
            Assert.That(result.ForceNuget, Is.True);
        }

        [Test]
        public void ShowHelp()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.Help }, null, null);

            //assert
            Assert.That(result.ShowHelp, Is.True);
        }

        [Test]
        public void PreferNuget()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.PreferNuget }, null, null);

            //assert
            Assert.That(result.PreferNuget, Is.True);
        }

        [Test]
        public void PreferNuget_FromAppSettings()
        {
            //arrange
            var appSettings = new NameValueCollection();
            appSettings.Add(ArgumentParser.AppSettingKeys.PreferNuget, "TrUe");

            //act
            var result = Parse(new string[] {}, appSettings, null);

            //assert
            Assert.That(result.PreferNuget, Is.True);
        }

        [Test]
        public void IgnoreCache()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.IgnoreCache }, null, null);

            //assert
            Assert.That(result.DownloadArguments.IgnoreCache, Is.True);
        }

        [Test]
        public void NugetSource()
        {
            //arrange

            //act
            var result = Parse(new[] { String.Format("{0}anySource", ArgumentParser.CommandArgs.NugetSourceArgPrefix) }, null, null);

            //assert
            Assert.That(result.DownloadArguments.NugetSource, Is.EqualTo("anySource"));
        }

        [Test]
        public void Prerelease()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.Prerelease }, null, null);

            //assert
            Assert.That(result.DownloadArguments.IgnorePrerelease, Is.False);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void Prerelease_FromAppSettings()
        {
            //arrange
            var appSettings = new NameValueCollection();
            appSettings.Add(ArgumentParser.AppSettingKeys.Prerelease, "TrUe");

            //act
            var result = Parse(new string[] { }, appSettings, null);

            //assert
            Assert.That(result.DownloadArguments.IgnorePrerelease, Is.False);
        }

        [Test]
        public void SelfUpdate()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.SelfUpdate }, null, null);

            //assert
            Assert.That(result.DownloadArguments.DoSelfUpdate, Is.True);
        }

        [Test]
        public void MaxFileAgeInMinutes()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.MaxFileAge + "10" }, null, null);

            //assert
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.EqualTo(10));
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void MaxFileAgeInMinutes_No_Value()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.MaxFileAge }, null, null);

            //assert
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.Null);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void MaxFileAgeInMinutes_Non_Integer_Value()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.MaxFileAge+"FOO" }, null, null);

            //assert
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.Null);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void LatestVersion()
        {
            //arrange

            //act
            var result = Parse(new[] { "1.0" }, null, null);

            //assert
            Assert.That(result.DownloadArguments.LatestVersion, Is.EqualTo("1.0"));
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void LatestVersion_FromEnvironmentVariable()
        {
            //arrange
            var envVariables= new Dictionary<string, string>();
            envVariables.Add(ArgumentParser.EnvArgs.PaketVersionEnv, "1.0");

            //act
            var result = Parse(new string[] {}, null, envVariables);

            //assert
            Assert.That(result.DownloadArguments.LatestVersion, Is.EqualTo("1.0"));
        }

        [Test]
        public void LeftoverCommandArgs()
        {
            //arrange

            //act
            var result = Parse(new[] { "2.22", "leftover" }, null, null);

            //assert
            Assert.That(result.UnprocessedCommandArgs, Is.Not.Empty.And.EqualTo(new[] { "leftover" }));
        }

        [Test]
        public void NoLeftoverWhenValidArgument()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.SelfUpdate }, null, null);

            //assert
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void Run()
        {
            //arrange

            //act
            var result = Parse(new[] { ArgumentParser.CommandArgs.Run }, null, null);

            //assert
            Assert.That(result.Run, Is.True);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void Run_WithArgs()
        {
            //arrange

            //act
            var result = Parse(
                new[]
                {
                    ArgumentParser.CommandArgs.MaxFileAge + "10",
                    ArgumentParser.CommandArgs.Run,
                    "-s",
                    "--help",
                    "foo"
                }, null, null);
            
            //assert
            Assert.That(result.Run, Is.True);
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.EqualTo(10));
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void Magic()
        {
            //arrange

            //act
            var result = ParseMagic(new[] {"-s", "--help", "foo"}, null, null);

            //assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Silent, Is.True);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.Run, Is.True);
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.EqualTo(720));
            Assert.That(result.DownloadArguments.Target, Does.StartWith(MagicModeFileSystemSystem.GetTempPath()).And.EndsWith(".exe"));
        }

        [Test]
        public void Magic_prerelease()
        {
            //arrange
            var appSettings = new NameValueCollection();
            appSettings.Add(ArgumentParser.AppSettingKeys.Prerelease, "TrUe");

            //act
            var result = ParseMagic(new[] {"-s", "--help", "foo"}, appSettings, null);

            //assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Silent, Is.True);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.Run, Is.True);
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.DownloadArguments.IgnorePrerelease, Is.False);
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.Null);
            Assert.That(result.DownloadArguments.Target, Does.StartWith(MagicModeFileSystemSystem.GetTempPath()).And.EndsWith(".exe"));
        }

        [Test]
        public void Magic_forced_version()
        {
            //arrange
            var appSettings = new NameValueCollection();
            appSettings.Add(ArgumentParser.AppSettingKeys.PaketVersion, "1.42");

            //act
            var result = ParseMagic(new[] {"-s", "--help", "foo"}, appSettings, null);

            //assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Silent, Is.True);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.Run, Is.True);
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.DownloadArguments.LatestVersion, Is.EqualTo("1.42"));
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.Null);
            Assert.That(result.DownloadArguments.Target, Does.StartWith(MagicModeFileSystemSystem.GetTempPath()).And.EndsWith(".exe"));
        }

        [Test]
        public void Magic_WithRun()
        {
            //arrange

            //act
            var result = ParseMagic(
                new[]
                {
                    ArgumentParser.CommandArgs.Silent,
                    ArgumentParser.CommandArgs.Run,
                    "-s",
                    "--help",
                    "foo"
                }, null, null);
            
            //assert
            Assert.That(result.Run, Is.True);
            Assert.That(result.Silent, Is.True);
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.DownloadArguments.Target, Does.StartWith(MagicModeFileSystemSystem.GetTempPath()).And.EndsWith(".exe"));
        }

        [Test]
        public void Magic_Dependencies_Empty_Args()
        {
            //arrange

            //act
            var result = ParseMagic(new[] {"-s", "--help", "foo"}, null, null,
                new string[0] );

            //assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Silent, Is.True);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.Run, Is.True);
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.EqualTo(720));
            Assert.That(result.DownloadArguments.Target, Does.StartWith(Path.GetTempPath()).And.EndsWith(".exe"));
        }

        [Test]
        public void Magic_Dependencies_Args()
        {
            //arrange

            //act
            var result = ParseMagic(new[] {"-s", "--help", "foo"}, null, null,
                new [] { "prerelease", "--max-file-age=42", "--nuget-source=http://local.site/path/here", "--force-nuget", "--prefer-nuget", "-f" } );

            //assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Silent, Is.True);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.Run, Is.True);
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.EqualTo(42));
            Assert.That(result.DownloadArguments.Target, Does.StartWith(Path.GetTempPath()).And.EndsWith(".exe"));
            Assert.That(result.DownloadArguments.IgnorePrerelease, Is.False);
            Assert.That(result.DownloadArguments.NugetSource, Is.EqualTo("http://local.site/path/here"));
            Assert.That(result.DownloadArguments.IgnoreCache, Is.True);
            Assert.That(result.ForceNuget, Is.True);
            Assert.That(result.PreferNuget, Is.True);
        }

        [Test]
        public void Magic_WithRun_Dependencies_Args()
        {
            //arrange

            //act
            var result = ParseMagic(
                new[]
                {
                    ArgumentParser.CommandArgs.Silent,
                    ArgumentParser.CommandArgs.Run,
                    "-s",
                    "--help",
                    "foo"
                }, null, null,
                new [] { "prerelease", "--max-file-age=42", "--nuget-source=http://local.site/path/here", "--force-nuget", "--prefer-nuget", "-f" });
            
            //assert
            Assert.That(result.Run, Is.True);
            Assert.That(result.Silent, Is.True);
            Assert.That(result.RunArgs, Is.Not.Empty.And.EqualTo(new[] {"-s", "--help", "foo"}));
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
            Assert.That(result.DownloadArguments.Target, Does.StartWith(Path.GetTempPath()).And.EndsWith(".exe"));
            Assert.That(result.DownloadArguments.MaxFileAgeInMinutes, Is.Null);
        }
    }
}
