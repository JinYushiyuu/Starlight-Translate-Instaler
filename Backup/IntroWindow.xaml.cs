using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Starlight_Translate_Instalador
{
    public partial class IntroWindow : Window
    {
        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "starlight_download_path.txt");

        public IntroWindow()
        {
            InitializeComponent();
            CarregarImagemAleatoria();
        }

        private void CarregarImagemAleatoria()
        {
            try
            {
                string[] imagens = { 
                    "img/Starlight-slider/BR.jpg", 
                    "img/Starlight-slider/Estelle.jpg", 
                    "img/Starlight-slider/Rise.png", 
                    "img/Starlight-slider/Ryza.jpg", 
                    "img/Starlight-slider/Sophie.jpg" 
                };

                Random rnd = new Random();
                string imagemEscolhida = imagens[rnd.Next(imagens.Length)];
                string caminhoCompleto = Path.GetFullPath(imagemEscolhida);

                if (File.Exists(caminhoCompleto))
                {
                    ImgSlider.Source = new BitmapImage(new Uri(caminhoCompleto, UriKind.Absolute));
                }
            }
            catch { }
        }

        private void BtnIniciar_Click(object sender, RoutedEventArgs e)
        {
            // Se já existe pasta salva, pula a configuração e abre direto o launcher
            if (File.Exists(ConfigFilePath))
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                // Se for primeira vez, obriga a escolher a pasta
                AbrirJanelaConfiguracao(true);
            }
        }

        private void BtnConfig_Click(object sender, RoutedEventArgs e)
        {
            // Clicar na engrenagem permite alterar a pasta a qualquer momento
            AbrirJanelaConfiguracao(false);
        }

        private void AbrirJanelaConfiguracao(bool iniciarAposSalvar)
        {
            string caminhoAtual = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Starlight_Downloads");
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string salvo = File.ReadAllText(ConfigFilePath).Trim();
                    if (!string.IsNullOrEmpty(salvo)) caminhoAtual = salvo;
                }
            }
            catch {}

            var janelaPasta = new PastaDownloadWindow(caminhoAtual);
            janelaPasta.Owner = this;
            
            if (janelaPasta.ShowDialog() == true)
            {
                try
                {
                    Directory.CreateDirectory(janelaPasta.CaminhoEscolhido);
                    File.WriteAllText(ConfigFilePath, janelaPasta.CaminhoEscolhido);

                    if (iniciarAposSalvar)
                    {
                        var mainWindow = new MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                }
                catch { }
            }
        }
    }

    public class PastaDownloadWindow : Window
    {
        private TextBlock _txtCaminho;
        private string _caminhoEscolhido = "";
        public string CaminhoEscolhido => _caminhoEscolhido;

        public PastaDownloadWindow(string caminhoInicial)
        {
            _caminhoEscolhido = caminhoInicial;
            Title = "Starlight Translate Instaler - Configuração";
            Width = 540;
            Height = 260;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)new BrushConverter().ConvertFrom("#1e1e24");
            Foreground = Brushes.White;
            ResizeMode = ResizeMode.NoResize;

            var stack = new StackPanel { Margin = new Thickness(25) };
            
            var txtTitulo = new TextBlock
            {
                Text = "Qual pasta você deseja fazer o download das traduções?",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(txtTitulo);

            var txtObs = new TextBlock
            {
                Text = "Obs: esse download será deletado após instalar",
                FontSize = 11,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#aeb4bd"),
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(txtObs);

            var panelPath = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            
            _txtCaminho = new TextBlock
            {
                Text = _caminhoEscolhido,
                Width = 420,
                Height = 32,
                Padding = new Thickness(8, 7, 8, 0),
                Background = (Brush)new BrushConverter().ConvertFrom("#161920"),
                Foreground = Brushes.White,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            panelPath.Children.Add(_txtCaminho);

            var btnBrowse = new Button
            {
                Content = "📁",
                Width = 45,
                Height = 32,
                Background = (Brush)new BrushConverter().ConvertFrom("#2a2e38"),
                Foreground = Brushes.White,
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0)
            };
            btnBrowse.Click += (s, e) => {
                var dialog = new OpenFolderDialog();
                dialog.Title = "Selecione a pasta para downloads";
                if (dialog.ShowDialog() == true)
                {
                    _txtCaminho.Text = dialog.FolderName;
                    _caminhoEscolhido = dialog.FolderName;
                }
            };
            panelPath.Children.Add(btnBrowse);
            stack.Children.Add(panelPath);

            var btnConfirmar = new Button
            {
                Content = "CONFIRMAR",
                Height = 40,
                Background = (Brush)new BrushConverter().ConvertFrom("#47b900"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            btnConfirmar.Click += (s, e) => {
                _caminhoEscolhido = _txtCaminho.Text;
                DialogResult = true;
                Close();
            };
            stack.Children.Add(btnConfirmar);

            Content = stack;
        }
    }
}