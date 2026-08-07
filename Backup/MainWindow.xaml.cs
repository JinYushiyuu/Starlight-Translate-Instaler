using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Win32;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media; 
using System.Windows.Media.Imaging;

namespace Starlight_Translate_Instalador
{
    public class Jogo
    {
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string Banner { get; set; } = "";
        public string UrlDownload { get; set; } = ""; 
        public string CaminhoIcone { get; set; } = "";
        public string CaminhoLogo { get; set; } = ""; 
        bool _disponivel = true;
        public bool Disponivel { get => _disponivel; set => _disponivel = value; } 
        
        public string StatusTraducao { get; set; } = "Em andamento";
        public string CorStatus { get; set; } = "#e74c3c";

        public List<string> TraducaoRevisaoLista { get; set; } = new List<string>();
        public List<string> DesignGraficoLista { get; set; } = new List<string>();
        public List<string> FerramentasLista { get; set; } = new List<string>();
        public List<string> OutrosLista { get; set; } = new List<string>();
        public bool TemCreditos => TraducaoRevisaoLista.Count > 0 || DesignGraficoLista.Count > 0 || FerramentasLista.Count > 0 || OutrosLista.Count > 0;

        public List<string> ArquivosObrigatorios { get; set; } = new List<string>();

        public double PctTraduzido { get; set; } 
        public double PctTraducao { get => PctTraduzido; set => PctTraduzido = value; }

        public double PctRevisado { get; set; }
        public double PctRevisao { get => PctRevisado; set => PctRevisado = value; }

        public Thickness PosicaoBanner { get; set; } = new Thickness(0, 0, 0, 0);

        public object? Icone 
        { 
            get 
            {
                if (string.IsNullOrEmpty(CaminhoIcone)) return null;
                try 
                {
                    if (CaminhoIcone.StartsWith("http")) 
                        return new BitmapImage(new Uri(CaminhoIcone, UriKind.Absolute));

                    var uri = CaminhoRecurso.MontarUri(CaminhoIcone);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG ICONE] Tentando carregar: {uri}");
                    var bmp = new BitmapImage(uri);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG ICONE] Sucesso: {uri}");
                    return bmp;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG ICONE] FALHOU para '{CaminhoIcone}': {ex.GetType().Name} - {ex.Message}");
                }
                return null;
            } 
        }
    }

    public static class CaminhoRecurso
    {
        public static Uri MontarUri(string caminhoRelativo)
        {
            string normalizado = caminhoRelativo.Replace('\\', '/').TrimStart('/');
            return new Uri($"pack://application:,,,/{normalizado}", UriKind.Absolute);
        }
    }

    public static class EstadoInstalacoes
    {
        private static string ArquivoEstado => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "starlight_instalacoes.json");

        public static Dictionary<string, string> Carregar()
        {
            try
            {
                if (File.Exists(ArquivoEstado))
                {
                    string json = File.ReadAllText(ArquivoEstado);
                    var dados = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dados != null) return dados;
                }
            }
            catch { }
            return new Dictionary<string, string>();
        }

        public static void Salvar(string nomeJogo, string caminhoPasta)
        {
            try
            {
                var dados = Carregar();
                dados[nomeJogo] = caminhoPasta;
                File.WriteAllText(ArquivoEstado, System.Text.Json.JsonSerializer.Serialize(dados));
            }
            catch { }
        }

        public static void Remover(string nomeJogo)
        {
            try
            {
                var dados = Carregar();
                if (dados.Remove(nomeJogo))
                {
                    File.WriteAllText(ArquivoEstado, System.Text.Json.JsonSerializer.Serialize(dados));
                }
            }
            catch { }
        }

        public static string? ObterCaminhoValido(string nomeJogo)
        {
            var dados = Carregar();
            if (dados.TryGetValue(nomeJogo, out string? caminho) && !string.IsNullOrEmpty(caminho))
            {
                string pastaBackup = Path.Combine(caminho, "Starlight_Backup");
                if (Directory.Exists(caminho) && Directory.Exists(pastaBackup))
                {
                    return caminho;
                }
                Remover(nomeJogo);
            }
            return null;
        }
    }

    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cts;
        private bool _estaPausado = false;
        private HttpClient _httpClient = new HttpClient();
        private bool _downloadConcluido = false;
        private string _ultimoArquivoBaixado = "";

        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "starlight_download_path.txt");

        public MainWindow()
        {
            InitializeComponent();
            CarregarListaDeJogos();
        }

        private static string ObterNomeArquivoZip(string nomeJogo)
        {
            string nomeLimpo = nomeJogo;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                nomeLimpo = nomeLimpo.Replace(c.ToString(), "");
            }
            return nomeLimpo.Trim() + ".zip";
        }

        public static string ObterCaminhoDownload()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string caminho = File.ReadAllText(ConfigFilePath).Trim();
                    if (!string.IsNullOrEmpty(caminho))
                    {
                        Directory.CreateDirectory(caminho);
                        return caminho;
                    }
                }
            }
            catch {}

            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Starlight_Downloads");
            Directory.CreateDirectory(defaultPath);
            return defaultPath;
        }

        private void CarregarListaDeJogos()
        {
            var jogos = new List<Jogo>
            {
                new Jogo 
                { 
                    Nome = "Atelier Ryza: Ever Darkness & the Secret Hideout DX",
                    Descricao = "O conceito deste título, o mais recente da série a apresentar um novo mundo \"Atelier\", gira em torno de \"adolescentes que crescem juntos, mesmo que apenas um pouco\".\nÉ a história de uma rapariga e dos seus amigos que estão prestes a entrar na idade adulta e a descobrir o que é mais importante para eles.",
                    CaminhoIcone = "img/icons/Ryza1.jpg", 
                    Banner = "img/banner/Ryza-banner.jpg", 
                    CaminhoLogo = @"img\Starlight-logo\logo.png", 
                    PosicaoBanner = new Thickness(-150, -80, 0, 0),
                    UrlDownload = "https://drive.google.com/uc?export=download&id=1hzlCKOl4ixSNPN1OPBgCxFVlyHbv4d77",
                    Disponivel = true,
                    StatusTraducao = "Completa",
                    CorStatus = "#2ecc71",
                    PctTraduzido = 1.0,
                    PctRevisado = 0.3,
                    TraducaoRevisaoLista = new List<string> { "Shiro Ysgarmr", "Alioth", "Jin_Yushiyuu" },
                    DesignGraficoLista = new List<string> { "Shiro Ysgarmr" },
                    FerramentasLista = new List<string> { "Jin_Yushiyuu" },
                    OutrosLista = new List<string> { "Linkmadao (Brazil Alliance)", "Lopez", "V.Slynx", "Rasec", "Abu (Phantasie Translate)", "Yami" },
                    ArquivosObrigatorios = new List<string>
                    {
                        "Atelier_Ryza_DX.exe",
                        @"Data\PACK01.PAK",
                        @"Data\PACK02.PAK",
                        @"Data\PACK00_04_01.PAK"
                    }
                },
                new Jogo 
                { 
                    Nome = "Atelier Ryza 2: Lost Legends & the Secret Fairy DX", 
                    Descricao = "Esta história decorre três anos antes dos eventos do jogo anterior \"Atelier Ryza: Ever Darkness & the Secret Hideout\", e retrata a reunião de Ryza com os seus amigos, numa aventura entre despedidas e novas amizades em busca de um tesouro de valor incalculável.",
                    CaminhoIcone = "img/icons/Ryza2.jpg",
                    Banner = "img/banner/Ryza2.jpg",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em andamento",
                    CorStatus = "#e74c3c",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Atelier Ryza 3: Alchemist of the End & the Secret Key DX", 
                    Descricao = "Tudo começa quando o arquipélago Kark Isles aparece ao lado da terra natal de Ryza, a protagonista. Com receio que seja uma ameaça ao seu lar, Ryza e amigos investigam as ilhas, deparando-se com ruínas e um portão enorme.",
                    CaminhoIcone = "img/icons/Ryza3.jpg",
                    Banner = "img/banner/Ryza3.avif",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em andamento",
                    CorStatus = "#e74c3c",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Atelier Sophie: The Alchemist of the Mysterious Book DX", 
                    Descricao = "\"Atelier Sophie\", o primeiro da série, regressa na versão DX! Sophie, uma jovem alquimista, cruza-se acidentalmente com um livro vivo chamado Plachta.",
                    CaminhoIcone = "img/icons/SophieDX.jpg",
                    Banner = "img/banner/SophieDX-Banner.avif",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em andamento",
                    CorStatus = "#e74c3c",
                    PctTraduzido = 0.3,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Atelier Sophie 2: The Alchemist of the Mysterious Dream", 
                    Descricao = "Uma história peculiar sobre um sonho misterioso... Esta é a história de uma das aventuras que Sophie, a Alquimista, teve após partir da sua terra natal de Kirchen Bell.",
                    CaminhoIcone = "img/icons/Sophie2.jpg",
                    Banner = "img/banner/Sophie2.jpg",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em breve",
                    CorStatus = "#3498db",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Atelier Yumia: The Alchemist of Memories & the Envisioned Land", 
                    Descricao = "Atelier Yumia é uma história em que Yumia e as suas companhias confrontam memórias e avançam no trilho em que acreditam.",
                    CaminhoIcone = "img/icons/Yumia.jpg",
                    Banner = "img/banner/Yumia.jpg",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em breve",
                    CorStatus = "#3498db",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Atelier Marie Remake: The Alchemist of Salburg", 
                    Descricao = "Não se trata de salvar o mundo. Marie, uma estudante em dificuldades que quer formar-se em alquimia, embarca em aventuras e missões ao mesmo tempo que tenta graduar-se.",
                    CaminhoIcone = "img/icons/Marie.jpg",
                    Banner = "img/banner/Marie.jpg",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em breve",
                    CorStatus = "#3498db",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Atelier Resleriana: The Red Alchemist & the White Guardian", 
                    Descricao = "A historia de dois protagonistas que reconstroem a sua cidade natal e procuram uma verdade escondida.",
                    CaminhoIcone = "img/icons/Resleriana.jpg",
                    Banner = "img/banner/Resleriana.avif",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em breve",
                    CorStatus = "#3498db",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Trails in the Sky 1st Chapter", 
                    Descricao = "Trails in the Sky 1º Capítulo reimagina o primeiro capítulo de uma série amada, adicionando visuais aprimorados e jogabilidade refinada.",
                    CaminhoIcone = "img/icons/Estelle.jpg",
                    Banner = "img/banner/sky1st.jpg",
                    PosicaoBanner = new Thickness(0, -80, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em andamento",
                    CorStatus = "#e74c3c",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "BLUE REFLECTION Quartet", 
                    Descricao = "BLUE REFLECTION Quartet reúne quatro histórias emocionantes numa só coleção.",
                    CaminhoIcone = "img/icons/BRQ.png",
                    Banner = "img/banner/BRQ.jpg",
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = false,
                    StatusTraducao = "Em breve",
                    CorStatus = "#3498db",
                    PctTraduzido = 0.0,
                    PctRevisado = 0.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
                new Jogo 
                { 
                    Nome = "Persona 4 Golden", 
                    Descricao = "Uma história de amadurecimento que coloca o protagonista e seus amigos em uma jornada iniciada por uma série de assassinatos em série.",
                    CaminhoIcone = "img/icons/P4G.jpg",
                    Banner = "img/banner/P4G.jpeg",
                    CaminhoLogo = @"img\Phantasie-Translate\logo.png", 
                    PosicaoBanner = new Thickness(0, -50, 0, 0),
                    UrlDownload = "",
                    Disponivel = true,
                    StatusTraducao = "Completa",
                    CorStatus = "#2ecc71",
                    PctTraduzido = 1.0,
                    PctRevisado = 0.80,
                    TraducaoRevisaoLista = new List<string> { "Ahther", "xtremegat", "Trots", "PHSticks", "Relat", "Sakamoto", "Uão", "Piloot", "Jonathan", "Belmont", "Hinrong" },
                    DesignGraficoLista = new List<string> { "Bruno \"ShadicDzn\" Luiz", "Uão", "Jau", "8Giga" },
                    FerramentasLista = new List<string> {},
                    OutrosLista = new List<string> { "Em breve mais colaboradores..." },
                    ArquivosObrigatorios = new List<string> { "p4g.exe", "data.cpk" }
                },
                new Jogo 
                { 
                    Nome = "KINGDOM HEARTS III", 
                    Descricao = "KINGDOM HEARTS III + Re Mind (DLC) encerra um capítulo da série. Viaje para novos e empolgantes mundos da Disney e Pixar e se prepare para o confronto final.",
                    CaminhoIcone = "img/icons/Sora.jpg",
                    Banner = "img/banner/KH.jpg",
                    PosicaoBanner = new Thickness(0, -10, 0, 0),
                    UrlDownload = "",
                    Disponivel = true,
                    StatusTraducao = "Em breve",
                    CorStatus = "#3498db",
                    PctTraduzido = 1.0,
                    PctRevisado = 1.0,
                    ArquivosObrigatorios = new List<string> { "*.exe" }
                },
            };

            ListaJogos.ItemsSource = jogos;

            if (jogos.Count > 0)
            {
                ListaJogos.SelectedIndex = 0;
            }
        }

        private void ListaJogos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaJogos.SelectedItem is Jogo jogoSelecionado)
            {
                TituloJogo.Text = jogoSelecionado.Nome;
                DescricaoJogo.Text = jogoSelecionado.Descricao;
                
                TxtStatusTraducao.Text = jogoSelecionado.StatusTraducao;
                TagStatusBorder.Background = (Brush)new BrushConverter().ConvertFrom(jogoSelecionado.CorStatus);
                TagStatusBorder.Visibility = Visibility.Visible;

                if (jogoSelecionado.TemCreditos)
                {
                    PainelCreditos.Visibility = Visibility.Visible;
                    ListaTraducaoRevisao.ItemsSource = jogoSelecionado.TraducaoRevisaoLista;
                    ListaDesignGrafico.ItemsSource = jogoSelecionado.DesignGraficoLista;
                    ListaFerramentas.ItemsSource = jogoSelecionado.FerramentasLista;
                    ListaOutros.ItemsSource = jogoSelecionado.OutrosLista;

                    try
                    {
                        string caminhoLogo = jogoSelecionado.CaminhoLogo;
                        if (!string.IsNullOrEmpty(caminhoLogo))
                        {
                            ImgLogoJogo.Source = new BitmapImage(CaminhoRecurso.MontarUri(caminhoLogo));
                            ImgLogoJogo.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            ImgLogoJogo.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch
                    {
                        ImgLogoJogo.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    PainelCreditos.Visibility = Visibility.Collapsed;
                    ImgLogoJogo.Visibility = Visibility.Collapsed;
                }

                try 
                {
                    string caminhoBanner = jogoSelecionado.Banner;
                    if (!string.IsNullOrEmpty(caminhoBanner))
                    {
                        Uri uriBanner = caminhoBanner.StartsWith("http")
                            ? new Uri(caminhoBanner, UriKind.Absolute)
                            : CaminhoRecurso.MontarUri(caminhoBanner);

                        System.Diagnostics.Debug.WriteLine($"[DEBUG BANNER] Tentando carregar: {uriBanner}");
                        ImgBanner.Source = new BitmapImage(uriBanner);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG BANNER] Sucesso: {uriBanner}");
                    }
                    
                    ImgBanner.Margin = jogoSelecionado.PosicaoBanner;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG BANNER] FALHOU para '{jogoSelecionado.Banner}': {ex.GetType().Name} - {ex.Message}");
                }
                
                PainelProgresso.Visibility = Visibility.Visible;
                
                TxtPctTraducao.Text = $"{Math.Round(jogoSelecionado.PctTraduzido * 100)}%";
                TxtPctRevisao.Text = $"{Math.Round(jogoSelecionado.PctRevisado * 100)}%";
                
                BarraTraducao.Width = 136 * jogoSelecionado.PctTraduzido;
                BarraRevisao.Width = 136 * jogoSelecionado.PctRevisado;

                TituloJogo.Visibility = Visibility.Visible;
                DescricaoJogo.Visibility = Visibility.Visible;
                BannerTxt.Visibility = Visibility.Hidden;

                PainelDownload.Visibility = Visibility.Hidden;
                BtnInstalar.Visibility = Visibility.Visible;
                _downloadConcluido = false;

                string? pastaJaInstalada = EstadoInstalacoes.ObterCaminhoValido(jogoSelecionado.Nome);

                if (pastaJaInstalada != null)
                {
                    BtnInstalar.Content = "GERENCIAR / DESINSTALAR";
                    BtnInstalar.Background = (Brush)new BrushConverter().ConvertFrom("#a83232");
                    BtnInstalar.Foreground = Brushes.White;
                    BtnInstalar.IsHitTestVisible = true;
                    BtnInstalar.Opacity = 1.0;
                }
                else if (jogoSelecionado.Disponivel)
                {
                    string caminhoZipExistente = Path.Combine(ObterCaminhoDownload(), ObterNomeArquivoZip(jogoSelecionado.Nome));

                    if (File.Exists(caminhoZipExistente))
                    {
                        _downloadConcluido = true;
                        _ultimoArquivoBaixado = caminhoZipExistente;

                        BtnInstalar.Content = "INSTALAR";
                        BtnInstalar.Background = (Brush)new BrushConverter().ConvertFrom("#2e86de");
                        BtnInstalar.Foreground = Brushes.White;
                        BtnInstalar.IsHitTestVisible = true;
                        BtnInstalar.Opacity = 1.0;
                    }
                    else
                    {
                        BtnInstalar.Content = "BAIXAR";
                        BtnInstalar.Background = (Brush)new BrushConverter().ConvertFrom("#47b900"); 
                        BtnInstalar.Foreground = Brushes.White; 
                        BtnInstalar.IsHitTestVisible = true; 
                        BtnInstalar.Opacity = 1.0;
                    }
                }
                else
                {
                    BtnInstalar.Content = "INDISPONÍVEL";
                    BtnInstalar.Background = (Brush)new BrushConverter().ConvertFrom("#2a2e38"); 
                    BtnInstalar.Foreground = (Brush)new BrushConverter().ConvertFrom("#767f8e"); 
                    BtnInstalar.IsHitTestVisible = false; 
                    BtnInstalar.Opacity = 0.8;
                }
            }
        }

        private async void BtnInstalar_Click(object sender, RoutedEventArgs e)
        {
            if (ListaJogos.SelectedItem is Jogo jogoSelecionado)
            {
                string? pastaJaInstalada = EstadoInstalacoes.ObterCaminhoValido(jogoSelecionado.Nome);
                if (pastaJaInstalada != null)
                {
                    var janelaGerenciar = new JanelaVerificacao(jogoSelecionado, "", pastaJaInstalada);
                    janelaGerenciar.Owner = this;
                    janelaGerenciar.ShowDialog();

                    ListaJogos_SelectionChanged(this, null!);
                    return;
                }

                if (!_downloadConcluido)
                {
                    BtnInstalar.Visibility = Visibility.Hidden;
                    PainelDownload.Visibility = Visibility.Visible;
                    BarraDownloadReal.Value = 0;
                    TxtStatusDownload.Text = "Iniciando download...";

                    _cts = new CancellationTokenSource();
                    _estaPausado = false;
                    BtnPausar.Content = "⏸";

                    try
                    {
                        string urlDestino = jogoSelecionado.UrlDownload;
                        string pastaDestinoDownload = ObterCaminhoDownload();
                        _ultimoArquivoBaixado = Path.Combine(pastaDestinoDownload, ObterNomeArquivoZip(jogoSelecionado.Nome));

                        if (urlDestino.Contains("drive.google.com") || urlDestino.Contains("drive.usercontent.google.com"))
                        {
                            using (var checkResponse = await _httpClient.GetAsync(urlDestino, HttpCompletionOption.ResponseHeadersRead, _cts.Token))
                            {
                                var contentType = checkResponse.Content.Headers.ContentType?.MediaType ?? "";
                                if (contentType.Contains("text/html"))
                                {
                                    var htmlContent = await checkResponse.Content.ReadAsStringAsync();

                                    var actionMatch = Regex.Match(htmlContent, @"<form[^>]*id=""download-form""[^>]*action=""([^""]+)""");
                                    string action = actionMatch.Success
                                        ? System.Net.WebUtility.HtmlDecode(actionMatch.Groups[1].Value)
                                        : "https://drive.usercontent.google.com/download";

                                    var inputMatches = Regex.Matches(htmlContent, @"<input[^>]*type=""hidden""[^>]*name=""([^""]+)""[^>]*value=""([^""]*)""");
                                    var parametros = new List<string>();
                                    foreach (Match m in inputMatches)
                                    {
                                        string nome = m.Groups[1].Value;
                                        string valor = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
                                        parametros.Add($"{Uri.EscapeDataString(nome)}={Uri.EscapeDataString(valor)}");
                                    }

                                    if (parametros.Count > 0)
                                    {
                                        urlDestino = action + "?" + string.Join("&", parametros);
                                    }
                                    else
                                    {
                                        var match = Regex.Match(htmlContent, @"confirm=([a-zA-Z0-9_-]+)");
                                        if (match.Success)
                                        {
                                            string confirmToken = match.Groups[1].Value;
                                            urlDestino += (urlDestino.Contains("?") ? "&" : "?") + $"confirm={confirmToken}";
                                        }
                                    }
                                }
                            }
                        }

                        using (var response = await _httpClient.GetAsync(urlDestino, HttpCompletionOption.ResponseHeadersRead, _cts.Token))
                        {
                            response.EnsureSuccessStatusCode();

                            var contentTypeFinal = response.Content.Headers.ContentType?.MediaType ?? "";
                            if (contentTypeFinal.Contains("text/html"))
                            {
                                throw new Exception("O Google Drive retornou uma página de aviso em vez do arquivo. Tente novamente mais tarde ou verifique o link.");
                            }

                            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                            
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(_ultimoArquivoBaixado, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                long totalBytesRead = 0;
                                int bytesRead;

                                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
                                {
                                    while (_estaPausado)
                                    {
                                        TxtStatusDownload.Text = "Download pausado.";
                                        await Task.Delay(500, _cts.Token);
                                    }

                                    _cts.Token.ThrowIfCancellationRequested();

                                    await fileStream.WriteAsync(buffer, 0, bytesRead, _cts.Token);
                                    totalBytesRead += bytesRead;

                                    if (totalBytes > 0)
                                    {
                                        double porcentagem = (double)totalBytesRead / totalBytes * 100;
                                        BarraDownloadReal.Value = porcentagem;
                                        TxtStatusDownload.Text = $"Baixando... {Math.Round(porcentagem)}% ({totalBytesRead / 1024 / 1024} MB)";
                                    }
                                    else
                                    {
                                        TxtStatusDownload.Text = $"Baixando... ({totalBytesRead / 1024 / 1024} MB)";
                                    }
                                }
                            }
                        }

                        TxtStatusDownload.Text = "Download concluído!";
                        await Task.Delay(1000);
                        PainelDownload.Visibility = Visibility.Hidden;

                        _downloadConcluido = true;
                        BtnInstalar.Content = "INSTALAR";
                        BtnInstalar.Background = (Brush)new BrushConverter().ConvertFrom("#2e86de");
                        BtnInstalar.Foreground = Brushes.White;
                        BtnInstalar.Visibility = Visibility.Visible;
                    }
                    catch (OperationCanceledException)
                    {
                        TxtStatusDownload.Text = "Download cancelado.";
                        await Task.Delay(1000);
                        PainelDownload.Visibility = Visibility.Hidden;
                        BtnInstalar.Visibility = Visibility.Visible;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(_ultimoArquivoBaixado) && File.Exists(_ultimoArquivoBaixado))
                            {
                                File.Delete(_ultimoArquivoBaixado);
                            }
                        }
                        catch { }

                        MessageBox.Show("Erro ao baixar o arquivo: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                        PainelDownload.Visibility = Visibility.Hidden;
                        BtnInstalar.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    var janelaVerificacao = new JanelaVerificacao(jogoSelecionado, _ultimoArquivoBaixado);
                    janelaVerificacao.Owner = this;
                    janelaVerificacao.ShowDialog();
                    
                    ListaJogos_SelectionChanged(this, null!);
                }
            }
        }

        private void BtnPausar_Click(object sender, RoutedEventArgs e)
        {
            _estaPausado = !_estaPausado;
            BtnPausar.Content = _estaPausado ? "▶" : "⏸";
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _estaPausado = false;
                _cts.Cancel();
            }
        }
    }

    public class JanelaVerificacao : Window
    {
        private string _caminhoPasta = "";
        private TextBlock _txtCaminho;
        private StackPanel _stackChecklistItens;
        private Button _btnProsseguir;
        private Jogo _jogo;
        private string _caminhoZip;
        private Dictionary<string, TextBlock> _statusChecks = new Dictionary<string, TextBlock>();
        private bool _jaEstaInstalado = false;

        public JanelaVerificacao(Jogo jogo, string caminhoZip, string? pastaConhecida = null)
        {
            _jogo = jogo;
            _caminhoZip = caminhoZip;
            Title = "Gerenciar Instalação - " + jogo.Nome;
            Width = 560;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)new BrushConverter().ConvertFrom("#1e1e24");
            Foreground = Brushes.White;
            ResizeMode = ResizeMode.NoResize;

            var gridPrincipal = new Grid { Margin = new Thickness(25) };
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var txtTitulo = new TextBlock
            {
                Text = "Gerenciamento de Arquivos da Tradução",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(txtTitulo, 0);
            gridPrincipal.Children.Add(txtTitulo);

            var painelEscolha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            
            var btnAuto = new Button
            {
                Content = "🔍 Procurar Automaticamente",
                Width = 230,
                Height = 35,
                Background = (Brush)new BrushConverter().ConvertFrom("#2a2e38"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnAuto.Click += (s, e) => ExecutarBuscaAutomatica();

            var btnManual = new Button
            {
                Content = "📂 Selecionar Manualmente",
                Width = 230,
                Height = 35,
                Background = (Brush)new BrushConverter().ConvertFrom("#2a2e38"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btnManual.Click += BtnExaminar_Click;

            painelEscolha.Children.Add(btnAuto);
            painelEscolha.Children.Add(btnManual);
            Grid.SetRow(painelEscolha, 1);
            gridPrincipal.Children.Add(painelEscolha);

            var painelPasta = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 15) };
            painelPasta.Children.Add(new TextBlock { Text = "Pasta Atual do Jogo:", Foreground = (Brush)new BrushConverter().ConvertFrom("#aeb4bd"), FontSize = 11, Margin = new Thickness(0, 0, 0, 3) });
            
            _txtCaminho = new TextBlock
            {
                Text = "Nenhuma pasta selecionada. Escolha acima.",
                Width = 470,
                Height = 30,
                Padding = new Thickness(8, 6, 8, 0),
                Background = (Brush)new BrushConverter().ConvertFrom("#161920"),
                Foreground = Brushes.White,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            painelPasta.Children.Add(_txtCaminho);
            Grid.SetRow(painelPasta, 2);
            gridPrincipal.Children.Add(painelPasta);

            var painelChecklist = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFrom("#161920"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var stackCheck = new StackPanel();
            stackCheck.Children.Add(new TextBlock { Text = "Status dos Arquivos do Jogo:", FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });

            _stackChecklistItens = new StackPanel();
            ConstruirChecklistDinamico();

            stackCheck.Children.Add(_stackChecklistItens);
            painelChecklist.Child = stackCheck;
            Grid.SetRow(painelChecklist, 3);
            gridPrincipal.Children.Add(painelChecklist);

            _btnProsseguir = new Button
            {
                Content = "INSTALAR TRADUÇÃO",
                Height = 42,
                Background = (Brush)new BrushConverter().ConvertFrom("#2a2e38"),
                Foreground = (Brush)new BrushConverter().ConvertFrom("#767f8e"),
                FontWeight = FontWeights.Bold,
                IsEnabled = false,
                Cursor = Cursors.No
            };
            _btnProsseguir.Click += BtnAcaoFinal_Click;
            Grid.SetRow(_btnProsseguir, 4);
            gridPrincipal.Children.Add(_btnProsseguir);

            Content = gridPrincipal;

            if (!string.IsNullOrEmpty(pastaConhecida) && Directory.Exists(pastaConhecida))
            {
                DefinirPasta(pastaConhecida);
            }
        }

        private void ConstruirChecklistDinamico()
        {
            _stackChecklistItens.Children.Clear();
            _statusChecks.Clear();

            foreach (var arquivo in _jogo.ArquivosObrigatorios)
            {
                var border = new Border { Margin = new Thickness(0, 4, 0, 4) };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock { Text = arquivo, Foreground = (Brush)new BrushConverter().ConvertFrom("#aeb4bd"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lbl, 0);
                grid.Children.Add(lbl);

                var txtStatus = new TextBlock { Text = "Aguardando...", Foreground = Brushes.Gray, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(txtStatus, 1);
                grid.Children.Add(txtStatus);

                border.Child = grid;
                _stackChecklistItens.Children.Add(border);
                _statusChecks[arquivo] = txtStatus;
            }
        }

        private async void ExecutarBuscaAutomatica()
        {
            _txtCaminho.Text = "Pesquisando nas bibliotecas da Steam...";
            SetStatusPesquisando();

            await Task.Delay(300);

            string pastaEncontrada =
                await Task.Run(() => LocalizarPastaSteamCommon(_jogo));

            // Se a Steam não indicou a pasta (registro ausente, VDF sem a
            // entrada certa, biblioteca em outro disco não detectada, etc.),
            // cai para uma varredura de verdade em todos os discos fixos.
            if (string.IsNullOrEmpty(pastaEncontrada) || !Directory.Exists(pastaEncontrada))
            {
                var progresso = new Progress<string>(pasta =>
                {
                    _txtCaminho.Text = $"Vasculhando: {pasta}";
                });

                pastaEncontrada =
                    await Task.Run(() => LocalizarPastaEmTodosDiscos(_jogo, progresso));
            }

            if (!string.IsNullOrEmpty(pastaEncontrada) && Directory.Exists(pastaEncontrada))
            {
                DefinirPasta(pastaEncontrada);
            }
            else
            {
                _txtCaminho.Text = "Pasta não encontrada automaticamente. Tente selecionar manualmente.";
                MarcarTodosComoNaoEncontrados();
            }
        }

        // Pastas que nunca vale a pena descer (evita perder tempo e travar
        // em áreas protegidas do sistema).
        private static readonly HashSet<string> _pastasIgnoradas = new(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "$Recycle.Bin", "System Volume Information",
            "ProgramData", "AppData", "Recovery", "PerfLogs",
            "Config.Msi", "MSOCache"
        };

        /// <summary>
        /// Varre todos os discos fixos do PC (SSD/HD internos e externos)
        /// procurando uma pasta que contenha todos os arquivos obrigatórios
        /// do jogo. É mais lento de propósito: uma varredura de disco real
        /// não acontece em 2 segundos.
        /// </summary>
        private string LocalizarPastaEmTodosDiscos(Jogo jogoAtual, IProgress<string>? progresso = null)
        {
            try
            {
                var arquivosObrigatorios = jogoAtual.ArquivosObrigatorios
                    .Where(a => !a.StartsWith("*"))
                    .ToList();

                if (arquivosObrigatorios.Count == 0)
                    return "";

                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                        continue;

                    string resultado = BuscarNaRaiz(drive.RootDirectory.FullName, arquivosObrigatorios, progresso, profundidadeMax: 6);
                    if (!string.IsNullOrEmpty(resultado))
                        return resultado;
                }
            }
            catch
            {
            }

            return "";
        }

        private string BuscarNaRaiz(string raiz, List<string> arquivosObrigatorios, IProgress<string>? progresso, int profundidadeMax)
        {
            // Busca em largura (fila) para achar a pasta do jogo o mais rápido
            // possível, sem estourar a pilha em árvores muito profundas.
            var fila = new Queue<(string pasta, int profundidade)>();
            fila.Enqueue((raiz, 0));

            while (fila.Count > 0)
            {
                var (pastaAtual, profundidade) = fila.Dequeue();

                if (profundidade > profundidadeMax)
                    continue;

                progresso?.Report(pastaAtual);

                // A própria pasta atual já satisfaz os requisitos?
                bool encontrouTudo = arquivosObrigatorios
                    .All(arquivo => File.Exists(Path.Combine(pastaAtual, arquivo)));

                if (encontrouTudo)
                    return pastaAtual;

                IEnumerable<string> subpastas;
                try
                {
                    subpastas = Directory.EnumerateDirectories(pastaAtual);
                }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }
                catch { continue; }

                foreach (string sub in subpastas)
                {
                    string nome = Path.GetFileName(sub);

                    if (_pastasIgnoradas.Contains(nome))
                        continue;
                    if (nome.StartsWith("."))
                        continue;

                    fila.Enqueue((sub, profundidade + 1));
                }
            }

            return "";
        }

        private string LocalizarPastaSteamCommon(Jogo jogoAtual)
        {
            try
            {
                List<string> locaisParaVerificar = new List<string>();

                // Steam principal
                string? caminhoSteam = null;

                RegistryKey? steamKey =
                    Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam") ??
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");

                if (steamKey != null)
                {
                    using (steamKey)
                    {
                        caminhoSteam = steamKey.GetValue("InstallPath")?.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(caminhoSteam))
                {
                    string common = Path.Combine(caminhoSteam, "steamapps", "common");

                    if (Directory.Exists(common))
                        locaisParaVerificar.Add(common);

                    string vdf = Path.Combine(caminhoSteam,
                        "steamapps",
                        "libraryfolders.vdf");

                    if (File.Exists(vdf))
                    {
                        string texto = File.ReadAllText(vdf);

                        foreach (Match m in Regex.Matches(texto, "\"path\"\\s*\"([^\"]+)\""))
                        {
                            string pasta =
                                m.Groups[1].Value.Replace(@"\\", @"\");

                            string commonLib =
                                Path.Combine(pasta, "steamapps", "common");

                            if (Directory.Exists(commonLib) &&
                                !locaisParaVerificar.Contains(commonLib))
                            {
                                locaisParaVerificar.Add(commonLib);
                            }
                        }
                    }
                }

                // Verifica cada biblioteca Steam
                foreach (string common in locaisParaVerificar)
                {
                    try
                    {
                        foreach (string pastaJogo in Directory.GetDirectories(common))
                        {
                            bool encontrouTudo = true;

                            foreach (string arquivo in jogoAtual.ArquivosObrigatorios)
                            {
                                if (arquivo.StartsWith("*"))
                                    continue;

                                string caminhoArquivo =
                                    Path.Combine(pastaJogo, arquivo);

                                if (!File.Exists(caminhoArquivo))
                                {
                                    encontrouTudo = false;
                                    break;
                                }
                            }

                            if (encontrouTudo)
                                return pastaJogo;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return "";
        }

        private void SetStatusPesquisando()
        {
            foreach (var kvp in _statusChecks)
            {
                kvp.Value.Text = "Pesquisando...";
                kvp.Value.Foreground = Brushes.Yellow;
            }
        }

        private void MarcarTodosComoNaoEncontrados()
        {
            foreach (var kvp in _statusChecks)
            {
                kvp.Value.Text = "❌ Não encontrado";
                kvp.Value.Foreground = (Brush)new BrushConverter().ConvertFrom("#a83232");
            }
        }

        private void BtnExaminar_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            dialog.Title = "Selecione a pasta raiz do jogo";
            if (dialog.ShowDialog() == true)
            {
                DefinirPasta(dialog.FolderName);
            }
        }

        private void DefinirPasta(string caminho)
        {
            _caminhoPasta = caminho;
            _txtCaminho.Text = caminho;

            bool tudoEncontrado = true;

            foreach (var reg in _jogo.ArquivosObrigatorios)
            {
                bool existe = false;
                if (reg.StartsWith("*"))
                {
                    string ext = reg.Substring(1);
                    existe = Directory.GetFiles(caminho, "*" + ext).Length > 0;
                }
                else
                {
                    string caminhoCompleto = Path.Combine(caminho, reg);
                    existe = File.Exists(caminhoCompleto);
                }

                SetStatusItem(_statusChecks[reg], existe);
                if (!existe) tudoEncontrado = false;
            }

            string pastaBackup = Path.Combine(_caminhoPasta, "Starlight_Backup");
            _jaEstaInstalado = Directory.Exists(pastaBackup);

            if (tudoEncontrado)
            {
                _btnProsseguir.IsEnabled = true;
                _btnProsseguir.Cursor = Cursors.Hand;

                if (_jaEstaInstalado)
                {
                    _btnProsseguir.Content = "DESINSTALAR TRADUÇÃO";
                    _btnProsseguir.Background = (Brush)new BrushConverter().ConvertFrom("#a83232");
                    _btnProsseguir.Foreground = Brushes.White;
                }
                else
                {
                    _btnProsseguir.Content = "INSTALAR TRADUÇÃO";
                    _btnProsseguir.Background = (Brush)new BrushConverter().ConvertFrom("#47b900");
                    _btnProsseguir.Foreground = Brushes.White;
                }
            }
            else
            {
                _btnProsseguir.Background = (Brush)new BrushConverter().ConvertFrom("#2a2e38");
                _btnProsseguir.Foreground = (Brush)new BrushConverter().ConvertFrom("#767f8e");
                _btnProsseguir.IsEnabled = false;
                _btnProsseguir.Cursor = Cursors.No;
            }
        }

        private void BtnAcaoFinal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pastaBackup = Path.Combine(_caminhoPasta, "Starlight_Backup");

                _jaEstaInstalado = Directory.Exists(pastaBackup);

                if (!_jaEstaInstalado)
                {
                    Directory.CreateDirectory(pastaBackup);

                    foreach (var reg in _jogo.ArquivosObrigatorios)
                    {
                        if (!reg.StartsWith("*"))
                        {
                            string arquivoOriginal = Path.Combine(_caminhoPasta, reg);
                            if (File.Exists(arquivoOriginal))
                            {
                                string destinoBackup = Path.Combine(pastaBackup, reg);
                                string? diretorioDestino = Path.GetDirectoryName(destinoBackup);
                                if (!string.IsNullOrEmpty(diretorioDestino))
                                {
                                    Directory.CreateDirectory(diretorioDestino);
                                }
                                File.Copy(arquivoOriginal, destinoBackup, true);
                            }
                        }
                    }

                    if (File.Exists(_caminhoZip))
                    {
                        ZipFile.ExtractToDirectory(_caminhoZip, _caminhoPasta, true);

                        try
                        {
                            File.Delete(_caminhoZip);
                        }
                        catch (Exception exDelete)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AVISO] Não foi possível apagar o zip: {exDelete.Message}");
                        }
                    }

                    EstadoInstalacoes.Salvar(_jogo.Nome, _caminhoPasta);

                    MessageBox.Show("Tradução instalada e extraída com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    foreach (var reg in _jogo.ArquivosObrigatorios)
                    {
                        if (!reg.StartsWith("*"))
                        {
                            string arquivoBackup = Path.Combine(pastaBackup, reg);
                            string arquivoOriginal = Path.Combine(_caminhoPasta, reg);

                            if (File.Exists(arquivoBackup))
                            {
                                File.Copy(arquivoBackup, arquivoOriginal, true);
                            }
                        }
                    }

                    if (Directory.Exists(pastaBackup))
                    {
                        var dirInfo = new DirectoryInfo(pastaBackup);
                        foreach (var info in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories))
                        {
                            info.Attributes = FileAttributes.Normal;
                        }
                        Directory.Delete(pastaBackup, true);
                    }

                    EstadoInstalacoes.Remover(_jogo.Nome);

                    MessageBox.Show("Tradução desinstalada e arquivos originais restaurados com sucesso!", "Desinstalação", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                Close();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Sem permissão para escrever na pasta do jogo. Isso costuma acontecer quando o jogo está instalado em 'Program Files' (pasta protegida pelo Windows).\n\n" +
                    "Feche o instalador e abra ele novamente clicando com o botão direito → 'Executar como administrador'.",
                    "Permissão negada", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao processar os arquivos: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetStatusItem(TextBlock txt, bool encontrado)
        {
            if (encontrado)
            {
                txt.Text = "✅ Encontrado";
                txt.Foreground = (Brush)new BrushConverter().ConvertFrom("#47b900");
            }
            else
            {
                txt.Text = "❌ Não encontrado";
                txt.Foreground = (Brush)new BrushConverter().ConvertFrom("#a83232");
            }
        }
    }
}