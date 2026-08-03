using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1715:Identifiers should have correct prefix",
    Justification = "Name required by the LawyerPlatform persistence marker contract.",
    Scope = "type",
    Target = "~T:LawyerPlatform.Application.Persistence.LawyerPlatformReadPersistence")]
[assembly: SuppressMessage(
    "Naming",
    "CA1715:Identifiers should have correct prefix",
    Justification = "Name required by the LawyerPlatform persistence marker contract.",
    Scope = "type",
    Target = "~T:LawyerPlatform.Application.Persistence.LawyerPlatformWritePersistence")]
