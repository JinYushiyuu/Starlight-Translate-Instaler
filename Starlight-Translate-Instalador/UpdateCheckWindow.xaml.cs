using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace Starlight_Translate_Instalador
{
    public partial class UpdateCheckWindow : Window
    {
        private const string GithubRepoUrl = "https://github.com/JinYushiyuu/Starlight-Translate-Instaler";

        public UpdateCheckWindow()
        {
            InitializeComponent();
            Loaded += UpdateCheckWindow_Loaded;
        }

        private async void UpdateCheckWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await VerificarAtualizacaoAsync();
        }

        private async Task VerificarAtualizacaoAsync()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(GithubRepoUrl, null, false));

                // Se o app não foi instalado via Velopack (ex: rodando direto pelo Visual Studio
                // em modo debug), não tem como aplicar update — pula direto pro app.
                if (!mgr.IsInstalled)
                {
                    AbrirIntro();
                    return;
                }

                var novaVersao = await mgr.CheckForUpdatesAsync();
                if (novaVersao == null)
                {
                    // Já está na versão mais recente
                    AbrirIntro();
                    return;
                }

                TxtStatus.Text = "Baixando atualização...";
                Progresso.IsIndeterminate = false;

                await mgr.DownloadUpdatesAsync(novaVersao, progresso =>
                    Dispatcher.Invoke(() => Progresso.Value = progresso));

                TxtStatus.Text = "Instalando atualização...";

                // Isso fecha o app, aplica a atualização e reabre a versão nova sozinho.
                // O código abaixo dessa linha normalmente não chega a executar.
                mgr.ApplyUpdatesAndRestart(novaVersao);
            }
            catch
            {
                // Sem internet, GitHub fora do ar, repositório ainda sem releases, etc:
                // nunca trava o usuário aqui, só segue pro app normalmente.
                AbrirIntro();
            }
        }

        private void AbrirIntro()
        {
            var intro = new IntroWindow();
            intro.Show();
            Close();
        }
    }
}