using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace KiLernprojekt;

public sealed class GpuBackend : IDisposable
{
    private readonly Context context;
    private readonly Accelerator accelerator;

    public string DeviceName => accelerator.Name;
    public bool IsCuda => accelerator.AcceleratorType == AcceleratorType.Cuda;

    public GpuBackend()
    {
        context = Context.Create(builder => builder.Cuda());
        accelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context);
    }

    public void Dispose()
    {
        accelerator.Dispose();
        context.Dispose();
    }
}
