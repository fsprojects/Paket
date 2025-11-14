namespace PaketTest2394.PackageA;

public static class PackageDescription
{
    public static string GetDescription()
    {
        var assemblyName = typeof(PackageDescription).Assembly.GetName();
        return $"PackageA {assemblyName.Version!.ToString(2)}";
    }
}
