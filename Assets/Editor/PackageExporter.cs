// Release-only dependencies are unavailable in the older-stream test projects.
#if UNITY_6000_6_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Release tooling, outside the package shipped to consumers.</summary>
public static class PackageExporter
{
    const string PackageName = "com.mygamedevtools.fixed-input";
    static readonly Regex MetaGuid = new Regex(@"^guid: ([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);

    /// <summary>Entry point used by unity-package-ci's unitypackage-export-method.</summary>
    public static void ExportPackage()
    {
        var organizationId = ReadOrganizationId(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable("UNITY_ORG_ID"));
        var packagePath = "Packages/" + PackageName;
        var manifest = JObject.Parse(File.ReadAllText(packagePath + "/package.json"));
        Export(packagePath, Path.GetFullPath($"{PackageName}-{manifest["version"]}.unitypackage"), organizationId);
    }

    static string ReadOrganizationId(string[] arguments, string environmentValue)
    {
        // GameCI v4 passes customParameters into its container; arbitrary env vars are not forwarded.
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] != "-unityPackageOrganizationId") continue;
            if (i + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[i + 1]) || arguments[i + 1].StartsWith("-"))
                throw new ArgumentException("-unityPackageOrganizationId requires a value.");
            return arguments[i + 1].Trim();
        }
        if (!string.IsNullOrWhiteSpace(environmentValue)) return environmentValue.Trim();
        throw new InvalidOperationException("Set UNITY_ORG_ID or pass -unityPackageOrganizationId to select the signing organization.");
    }

    public static void Export(string packagePath, string outputPath, string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
            throw new ArgumentException("A signing organization is required.", nameof(organizationId));

        packagePath = packagePath.Replace('\\', '/').TrimEnd('/');
        if (packagePath != "Packages/" + PackageName)
            throw new ArgumentException($"Expected Packages/{PackageName}.", nameof(packagePath));
        if (Directory.Exists(packagePath + "/Samples") && Directory.Exists(packagePath + "/Samples~"))
            throw new InvalidOperationException("Both Samples and Samples~ exist; refusing an ambiguous export.");

        var manifest = JObject.Parse(File.ReadAllText(packagePath + "/package.json"));
        if ((string)manifest["name"] != PackageName || string.IsNullOrWhiteSpace((string)manifest["version"]))
            throw new InvalidOperationException("The package manifest must declare the expected name and a version.");
        if (manifest["samples"] is JArray samples)
            foreach (var sample in samples)
                sample["path"] = HideSamples((string)sample["path"]);

        var temp = Path.Combine(Path.GetTempPath(), "fixed-input-export-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(temp, "contents");
        Directory.CreateDirectory(staging);
        try
        {
            // Work from disk: the AssetDatabase deliberately excludes Samples~.
            // Rename only archive paths, leaving the development project untouched.
            var guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Stage(packagePath, packagePath, staging, guids, null);
            foreach (var path in Directory.GetDirectories(packagePath, "*", SearchOption.AllDirectories))
                Stage(path, packagePath, staging, guids, null);
            foreach (var path in Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path) == ".DS_Store")
                    continue;
                Stage(path, packagePath, staging, guids,
                    path.Replace('\\', '/') == packagePath + "/package.json" ? manifest.ToString() + "\n" : null);
            }

            var signedArchive = Path.Combine(temp, "signed.unitypackage");
            Sign(staging, signedArchive, organizationId);
            RequireAttestation(signedArchive);
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.Copy(signedArchive, outputPath, true);
            Debug.Log($"Exported signed package: {outputPath}");
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    static string HideSamples(string path)
    {
        if (path == "Samples") return "Samples~";
        return path != null && path.StartsWith("Samples/", StringComparison.Ordinal)
            ? "Samples~/" + path.Substring("Samples/".Length) : path;
    }

    static void Stage(string path, string packagePath, string staging, Dictionary<string, string> guids, string contents)
    {
        path = path.Replace('\\', '/');
        var metaPath = path + ".meta";
        string guid;
        if (File.Exists(metaPath))
        {
            var match = MetaGuid.Match(File.ReadAllText(metaPath));
            if (!match.Success) throw new InvalidDataException($"Missing or malformed GUID in {metaPath}.");
            guid = match.Groups[1].Value.ToLowerInvariant();
        }
        else
        {
            // Hidden folders and ordinary documentation can legitimately lack metadata.
            guid = Guid.NewGuid().ToString("N");
        }
        if (guids.TryGetValue(guid, out var previous))
            throw new InvalidDataException($"Duplicate GUID {guid}: {previous} and {path}.");
        guids.Add(guid, path);

        var destination = Path.Combine(staging, guid);
        Directory.CreateDirectory(destination);
        var relative = path == packagePath ? "" : path.Substring(packagePath.Length + 1);
        var archivePath = relative.Length == 0 ? packagePath : packagePath + "/" + HideSamples(relative);
        File.WriteAllText(Path.Combine(destination, "pathname"), archivePath, new UTF8Encoding(false));
        if (contents != null)
            File.WriteAllText(Path.Combine(destination, "asset"), contents, new UTF8Encoding(false));
        else if (File.Exists(path))
            File.Copy(path, Path.Combine(destination, "asset"));
        if (File.Exists(metaPath))
            File.Copy(metaPath, Path.Combine(destination, "asset.meta"));
    }

    static void Sign(string staging, string archive, string organizationId)
    {
        // Internal Unity 6.6 APIs. Resolve exact signatures and fail if Unity changes them.
        // CreateSignedAssetPackage catches signing errors in 6.6.3 and returns void.
        // Calling its two constituent steps lets a signing failure propagate to CI.
        var assembly = typeof(UnityEditor.AssetPackage.Package).Assembly;
        var tarball = assembly.GetType("UnityEditor.AssetPackage.Tarball", true);
        var signer = assembly.GetType("UnityEditor.AssetPackage.SignedAssetPackage", true);
        var serviceType = assembly.GetType("UnityEditor.AssetPackage.SignatureService", true);
        Method(tarball, "CreateTarballFromFolder", typeof(string), typeof(string))
            .Invoke(null, new object[] { staging, archive });
        var sign = Method(signer, "InsertAttestationFileIntoTarball",
            typeof(string), typeof(string), typeof(string), serviceType);

        // Capture the signed-in Editor identity on its main thread, then sign on a worker.
        // Blocking a main-thread async continuation would deadlock a batch export.
        var service = Activator.CreateInstance(serviceType, true);
        Task.Run(async () =>
        {
            var task = (Task)sign.Invoke(null, new[] { (object)archive, archive, organizationId, service });
            await task;
        }).GetAwaiter().GetResult();
    }

    static MethodInfo Method(Type type, string name, params Type[] parameters)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);
    }

    static void RequireAttestation(string archive)
    {
        // Unity's signer prepends the attestation as the first TAR entry.
        using (var stream = new GZipStream(File.OpenRead(archive), CompressionMode.Decompress))
        {
            var header = new byte[512];
            int offset = 0;
            while (offset < header.Length)
            {
                int count = stream.Read(header, offset, header.Length - offset);
                if (count == 0) throw new InvalidDataException("The signed archive is truncated.");
                offset += count;
            }
            var name = Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0');
            var size = Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
            if (name != "package/.attestation.p7m" || Convert.ToInt64(size, 8) <= 0)
                throw new InvalidDataException("Unity did not produce a package attestation; refusing an unsigned export.");
        }
    }
}
#else
public static class PackageExporter
{
    public static void ExportPackage()
    {
        throw new System.NotSupportedException("Signed .unitypackage export requires Unity 6.6 or newer.");
    }
}
#endif
