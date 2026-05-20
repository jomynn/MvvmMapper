using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Rendering;

public interface IRenderer
{
    string Format { get; }
    Task RenderAsync(MvvmGraph graph, string outputDirectory, CancellationToken cancellationToken = default);
}
