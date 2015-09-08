using System;
using System.Text.RegularExpressions;

namespace Paket.Bootstrapper
{
    public class SemVer : IComparable
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public PreRelease PreRelease { get; set; }
        public string Original { get; set; }
        public string PreReleaseBuild { get; set; }
        public string Build { get; set; }

        public int CompareTo(object obj)
        {
            var y = obj as SemVer;
            if (y == null)
                throw new ArgumentException("cannot compare values of different types", "obj");
            var x = this;
            if (x.Major != y.Major) return x.Major.CompareTo(y.Major);
            else if (x.Minor != y.Minor) return x.Minor.CompareTo(y.Minor);
            else if (x.Patch != y.Patch) return x.Patch.CompareTo(y.Patch);
            else if (x.Build != y.Build)
            {
                int buildx, buildy;
                var parseResultx = Int32.TryParse(x.Build, out buildx);
                var parseResulty = Int32.TryParse(y.Build, out buildy);
                if (parseResultx && parseResulty)
                    return buildx.CompareTo(buildy);
                return x.Build.CompareTo(y.Build);
            }
            else if (x.PreRelease == y.PreRelease && x.PreReleaseBuild == y.PreReleaseBuild) return 0;
            else if (x.PreRelease == null && y.PreRelease != null && x.PreReleaseBuild == "0") return 1;
            else if (y.PreRelease == null && x.PreRelease != null && y.PreReleaseBuild == "0") return -1;
            else if (x.PreRelease != y.PreRelease) return x.PreRelease.CompareTo(y.PreRelease);
            else if (x.PreReleaseBuild != y.PreReleaseBuild)
            {
                int prereleaseBuildx, prereleaseBuildy;
                var parseResultx = Int32.TryParse(x.PreReleaseBuild, out prereleaseBuildx);
                var parseResulty = Int32.TryParse(y.PreReleaseBuild, out prereleaseBuildy);
                if (parseResultx && parseResulty)
                    return prereleaseBuildx.CompareTo(prereleaseBuildy);
                return x.PreReleaseBuild.CompareTo(y.PreReleaseBuild);
            }
            else return 0;
        }

        public override bool Equals(object obj)
        {
            var y = obj as SemVer;
            if (y == null)
                return false;
            return Major == y.Major && Minor == y.Minor && Patch == y.Patch && PreRelease == y.PreRelease && Build == y.Build && PreReleaseBuild == y.PreReleaseBuild;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Major;
                hashCode = (hashCode * 397) ^ Minor;
                hashCode = (hashCode * 397) ^ Patch;
                hashCode = (hashCode * 397) ^ (PreRelease != null ? PreRelease.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Original != null ? Original.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (PreReleaseBuild != null ? PreReleaseBuild.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Build != null ? Build.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static SemVer Create(string version)
        {
            var dashSplitted = version.Split('-', '+');
            var splitted = dashSplitted[0].Split('.');
            var l = splitted.Length;

            var prereleaseBuild = dashSplitted.Length > 1 && dashSplitted[1].Split('.').Length > 1 ? dashSplitted[1].Split('.')[1] : "0";

            return new SemVer
            {
                Major = l > 0 ? Int32.Parse(splitted[0]) : 0,
                Minor = l > 1 ? Int32.Parse(splitted[1]) : 0,
                Patch = l > 2 ? Int32.Parse(splitted[2]) : 0,
                PreRelease = PreRelease.TryParse(dashSplitted.Length > 1 ? dashSplitted[1].Split('.')[0] : String.Empty),
                Build = l > 3 ? splitted[3] : "0",
                PreReleaseBuild = prereleaseBuild,
                Original = version
            };
        }
    }

    public class PreRelease : IComparable
    {
        public string Origin { get; set; }
        public string Name { get; set; }
        public long? Number { get; set; }

        public static PreRelease TryParse(string str)
        {
            var m = new Regex(@"^(?<name>[a-zA-Z]+)(?<number>\d*)$").Match(str);
            if (m.Success)
            {
                long? number = null;
                if (m.Groups["number"].Value != "")
                    number = long.Parse(m.Groups["number"].Value);

                return new PreRelease { Origin = str, Name = m.Groups["name"].Value, Number = number };
            }
            return null;
        }

        public int CompareTo(object obj)
        {
            var yObj = obj as PreRelease;
            if (yObj == null)
                throw new ArgumentException("cannot compare values of different types", "obj");
            var x = this;
            if (x.Name != yObj.Name) return x.Name.CompareTo(yObj.Name);
            else if (!x.Number.HasValue && yObj.Number.HasValue)
                return 1;
            else if (x.Number.HasValue && !yObj.Number.HasValue)
                return -1;
            else if (!x.Number.HasValue && !yObj.Number.HasValue)
                return 0;
            else
                return x.Number.Value.CompareTo(yObj.Number.Value);
        }
    }
}
