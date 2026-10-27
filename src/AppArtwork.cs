using System.Drawing;
using System.IO;
using System.Reflection;

namespace CampusNet
{
	internal static class AppArtwork
	{
		internal static Icon LoadIcon(int size)
		{
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("App.ico"))
			{
				using (Icon icon = new Icon(stream, new Size(size, size)))
				{
					return (Icon)icon.Clone();
				}
			}
		}

		internal static Bitmap LoadLogo()
		{
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Logo.png"))
			{
				using (Image original = Image.FromStream(stream))
				{
					return new Bitmap(original);
				}
			}
		}
	}
}
