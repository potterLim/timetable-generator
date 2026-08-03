using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation.Models;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed partial class SchedulePngBatchWriterTests
{
    private sealed class FailingCandidatePngExporter : IControlPngExporter
    {
        private readonly int mFailingCallNumber;

        public int ExportCallCount { get; private set; }

        public FailingCandidatePngExporter(int failingCallNumber)
        {
            mFailingCallNumber = failingCallNumber;
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportCallCount++;
            destinationStream.WriteByte(1);
            if (ExportCallCount == mFailingCallNumber)
            {
                throw new IOException("Synthetic candidate export failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ResponsiveRecordingPngExporter : IControlPngExporter
    {
        private readonly Func<bool> mReadInputResponsiveness;

        public int ExportCallCount { get; private set; }

        public bool InputWasResponsiveDuringBatch { get; private set; }

        public ResponsiveRecordingPngExporter(Func<bool> readInputResponsiveness)
        {
            if (readInputResponsiveness == null)
            {
                throw new ArgumentNullException(nameof(readInputResponsiveness));
            }

            mReadInputResponsiveness = readInputResponsiveness;
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportCallCount++;
            InputWasResponsiveDuringBatch |= mReadInputResponsiveness();

            destinationStream.WriteByte(1);
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingPngExporter : IControlPngExporter
    {
        private readonly CancellationTokenSource mCancellationSource;

        public CancellingPngExporter(CancellationTokenSource cancellationSource)
        {
            if (cancellationSource == null)
            {
                throw new ArgumentNullException(nameof(cancellationSource));
            }

            mCancellationSource = cancellationSource;
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destinationStream.WriteByte(1);
            mCancellationSource.Cancel();
            return Task.FromCanceled(mCancellationSource.Token);
        }
    }

    private sealed class RecordingPngExporter : IControlPngExporter
    {
        private readonly List<Control> mSurfaces = new List<Control>();

        private readonly List<ScheduleBoardLayout> mLayouts = new List<ScheduleBoardLayout>();

        private readonly List<double> mSurfaceWidths = new List<double>();

        public IReadOnlyList<Control> Surfaces
        {
            get
            {
                return mSurfaces.AsReadOnly();
            }
        }

        public IReadOnlyList<ScheduleBoardLayout> Layouts
        {
            get
            {
                return mLayouts.AsReadOnly();
            }
        }

        public IReadOnlyList<double> SurfaceWidths
        {
            get
            {
                return mSurfaceWidths.AsReadOnly();
            }
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mSurfaces.Add(sourceControl);
            mSurfaceWidths.Add(sourceControl.Bounds.Width);
            ScheduleBoardPresentation presentation = Assert.IsType<ScheduleBoardPresentation>(sourceControl.DataContext);
            mLayouts.Add(presentation.Layout);
            destinationStream.WriteByte(1);
            return Task.CompletedTask;
        }
    }
}
