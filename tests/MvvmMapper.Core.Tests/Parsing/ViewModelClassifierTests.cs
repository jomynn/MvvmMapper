using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Parsing;
using Xunit;

namespace MvvmMapper.Core.Tests.Parsing;

public sealed class ViewModelClassifierTests
{
    private static Compilation BuildCompilation(string source) =>
        CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    [Fact]
    public void IsViewModel_SuffixMatch_ReturnsTrue()
    {
        var compilation = BuildCompilation("public class LoginViewModel {}");
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        var type = compilation.GetTypeByMetadataName("LoginViewModel");
        type.Should().NotBeNull();
        classifier.IsViewModel(type!).Should().BeTrue();
    }

    [Fact]
    public void IsViewModel_NoSuffix_ReturnsFalse()
    {
        var compilation = BuildCompilation("public class AuthService {}");
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        var type = compilation.GetTypeByMetadataName("AuthService");
        type.Should().NotBeNull();
        classifier.IsViewModel(type!).Should().BeFalse();
    }

    [Fact]
    public void IsViewModel_VMSuffix_ReturnsTrue()
    {
        var compilation = BuildCompilation("public class LoginVM {}");
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        var type = compilation.GetTypeByMetadataName("LoginVM");
        type.Should().NotBeNull();
        classifier.IsViewModel(type!).Should().BeTrue();
    }

    [Fact]
    public void IsViewModel_AbstractClass_ReturnsFalse()
    {
        var compilation = BuildCompilation("public abstract class BaseViewModel {}");
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        var type = compilation.GetTypeByMetadataName("BaseViewModel");
        type.Should().NotBeNull();
        classifier.IsViewModel(type!).Should().BeFalse();
    }

    [Fact]
    public void IsViewModel_InheritsBaseType_ReturnsTrue()
    {
        const string source = """
            public class ObservableObject {}
            public class LoginService : ObservableObject {}
            """;
        var compilation = BuildCompilation(source);
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        var type = compilation.GetTypeByMetadataName("LoginService");
        type.Should().NotBeNull();
        // Inherits ObservableObject — classified as VM regardless of name
        classifier.IsViewModel(type!).Should().BeTrue();
    }

    [Fact]
    public void IsViewModel_Interface_ReturnsFalse()
    {
        var compilation = BuildCompilation("public interface ILoginViewModel {}");
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        var type = compilation.GetTypeByMetadataName("ILoginViewModel");
        type.Should().NotBeNull();
        classifier.IsViewModel(type!).Should().BeFalse();
    }

    [Fact]
    public void IsView_ViewSuffix_ReturnsTrue()
    {
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        classifier.IsView("LoginView").Should().BeTrue();
        classifier.IsView("SettingsPage").Should().BeTrue();
        classifier.IsView("MainWindow").Should().BeTrue();
    }

    [Fact]
    public void IsView_NoSuffix_ReturnsFalse()
    {
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        classifier.IsView("AuthService").Should().BeFalse();
    }

    [Fact]
    public void IsViewModel_TransitiveBaseType_ReturnsTrue()
    {
        const string source = """
            public class ObservableObject {}
            public class ViewModelBase : ObservableObject {}
            public class LoginService : ViewModelBase {}
            """;
        var compilation = BuildCompilation(source);
        var config = new PatternConfig();
        var classifier = new ViewModelClassifier(config);

        var type = compilation.GetTypeByMetadataName("LoginService");
        type.Should().NotBeNull();
        classifier.IsViewModel(type!).Should().BeTrue();
    }
}
