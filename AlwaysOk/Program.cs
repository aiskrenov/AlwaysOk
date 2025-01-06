namespace AlwaysOk;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenAnyIP(8080);

            serverOptions.ListenAnyIP(8081, listenOptions
                => listenOptions.UseHttps(httpsOptions
                    => httpsOptions.ServerCertificateSelector = (connectionContext, name)
                        => name is not null
                            ? CertificateFactory.GetCertificate(name)
                            : CertificateFactory.GetDefault()));
        });

        builder.Services.AddControllers();
        var app = builder.Build();
        app.MapControllers();
        app.Run();
    }
}
