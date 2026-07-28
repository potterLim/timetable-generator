using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;

namespace TimetableGenerator.Desktop.Exporting;

public interface IControlPngExporter
{
    Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken);
}
