using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using NUnit.Framework;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    public class ArgumentParserTests
    {

        [Test]
        public void NullArguments_GetDefault()
        {
            //arrange
            //act
            //assert
            Assert.Throws<ArgumentNullException>(() => ArgumentParser.ParseArgumentsAndConfigurations(null, null, null));
        }

        [Test]
        public void EmptyArguments_GetDefault()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new string[] { }, null, null);

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

            var knownProps = new[] { "DownloadArguments.Folder", "DownloadArguments.Target", "DownloadArguments.NugetSource", "DownloadArguments.DoSelfUpdate", "DownloadArguments.LatestVersion", "DownloadArguments.IgnorePrerelease", "DownloadArguments.IgnoreCache", "Silent", "ForceNuget", "PreferNuget", "UnprocessedCommandArgs" };
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
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { ArgumentParser.CommandArgs.Silent }, null, null);

            //assert
            Assert.That(result.Silent, Is.True);
        }

        [Test]
        public void ForceNuget()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { ArgumentParser.CommandArgs.ForceNuget }, null, null);

            //assert
            Assert.That(result.ForceNuget, Is.True);
        }

        [Test]
        public void ForceNuget_FromAppSettings()
        {
            //arrange
            var appSettings = new NameValueCollection();
            appSettings.Add(ArgumentParser.AppSettingKeys.ForceNugetAppSettingsKey, "TrUe");

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new string[] { }, appSettings, null);

            //assert
            Assert.That(result.ForceNuget, Is.True);
        }

        [Test]
        public void PreferNuget()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { ArgumentParser.CommandArgs.PreferNuget }, null, null);

            //assert
            Assert.That(result.PreferNuget, Is.True);
        }

        [Test]
        public void PreferNuget_FromAppSettings()
        {
            //arrange
            var appSettings = new NameValueCollection();
            appSettings.Add(ArgumentParser.AppSettingKeys.PreferNugetAppSettingsKey, "TrUe");

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new string[] {}, appSettings, null);

            //assert
            Assert.That(result.PreferNuget, Is.True);
        }

        [Test]
        public void IgnoreCache()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { ArgumentParser.CommandArgs.IgnoreCache }, null, null);

            //assert
            Assert.That(result.DownloadArguments.IgnoreCache, Is.True);
        }

        [Test]
        public void NugetSource()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { String.Format("{0}anySource", ArgumentParser.CommandArgs.NugetSourceArgPrefix) }, null, null);

            //assert
            Assert.That(result.DownloadArguments.NugetSource, Is.EqualTo("anySource"));
        }

        [Test]
        public void Prerelease()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { ArgumentParser.CommandArgs.Prerelease }, null, null);

            //assert
            Assert.That(result.DownloadArguments.IgnorePrerelease, Is.False);
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

        [Test]
        public void SelfUpdate()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { ArgumentParser.CommandArgs.SelfUpdate }, null, null);

            //assert
            Assert.That(result.DownloadArguments.DoSelfUpdate, Is.True);
        }

        [Test]
        public void LatestVersion()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { "1.0" }, null, null);

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
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new string[] {}, null, envVariables);

            //assert
            Assert.That(result.DownloadArguments.LatestVersion, Is.EqualTo("1.0"));
        }

        [Test]
        public void LeftoverCommandArgs()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { "2.22", "leftover" }, null, null);

            //assert
            Assert.That(result.UnprocessedCommandArgs, Is.Not.Empty.And.EqualTo(new[] { "leftover" }));
        }

        [Test]
        public void NoLeftoverWhenValidArgument()
        {
            //arrange

            //act
            var result = ArgumentParser.ParseArgumentsAndConfigurations(new[] { ArgumentParser.CommandArgs.SelfUpdate }, null, null);

            //assert
            Assert.That(result.UnprocessedCommandArgs, Is.Empty);
        }

    }
}
