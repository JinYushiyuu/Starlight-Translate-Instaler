using System;
using System.Windows;
using Velopack;

namespace Starlight_Translate_Instalador;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // Precisamos de um Main customizado (em vez do que o WPF gera sozinho)
    // porque o Velopack precisa rodar como a PRIMEIRA coisa da aplicação,
    // antes de qualquer janela ou código do WPF.
    [STAThread]
    private static void Main(string[] args)
    {
        // Trata os "hooks" de instalação/atualização do Velopack (primeira execução,
        // pós-instalação, pós-atualização, etc). Se o processo foi chamado pelo instalador/
        // updater com argumentos especiais, o app sai aqui mesmo, sem chegar a abrir janela.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Em vez de abrir a IntroWindow direto (como fazia o StartupUri),
        // abrimos primeiro a tela de checagem de atualização. Ela mesma decide
        // se abre a IntroWindow em seguida ou se baixa/aplica um update.
        var updateCheck = new UpdateCheckWindow();
        updateCheck.Show();
    }
}
