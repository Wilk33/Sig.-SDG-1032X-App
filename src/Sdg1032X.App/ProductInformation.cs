using System.IO;
using System.Reflection;

namespace Sdg1032X.App;

public static class ProductInformation
{
	private const string LicenseResourceName="Sdg1032X.LICENSE";

	public const string AuthorName="Mateusz Skipor";
	public const string AuthorProfession="Inżynier technik elektroniki";
	public const string AuthorEmail="mskiporsklep@op.pl";
	public const string AuthorText=
		AuthorName+"\n"+AuthorProfession+"\n"+AuthorEmail;

	public static string Version
	{
		get
		{
			Version? version=typeof(ProductInformation).Assembly.GetName().Version;
			return version is null
				? "0.0.0"
				: $"{version.Major}.{version.Minor}.{version.Build}";
		}
	}

	public static string DisplayName => "SIGLENT SDG1032X v"+Version;

	public static string GetWindowTitle(bool demoMode)
	{
		return demoMode ? DisplayName+" - DEMO" : DisplayName;
	}

	public static string LoadLicenseText()
	{
		Assembly assembly=typeof(ProductInformation).Assembly;
		using Stream stream=assembly.GetManifestResourceStream(LicenseResourceName) ??
			throw new InvalidOperationException("Nie znaleziono osadzonego tekstu licencji.");
		using StreamReader reader=new(stream);
		return reader.ReadToEnd();
	}
}
