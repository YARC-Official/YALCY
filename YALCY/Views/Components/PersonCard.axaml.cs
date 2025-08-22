using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using YALCY.Models;

namespace YALCY.Views.Components
{
    public partial class PersonCard : UserControl
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        static PersonCard()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "YALCY-App/1.0");
        }

        public static readonly StyledProperty<string> DisplayNameProperty =
            AvaloniaProperty.Register<PersonCard, string>(nameof(DisplayName));

        public static readonly StyledProperty<string> RoleProperty =
            AvaloniaProperty.Register<PersonCard, string>(nameof(Role));

        public static readonly StyledProperty<string> GitHubUsernameProperty =
            AvaloniaProperty.Register<PersonCard, string>(nameof(GitHubUsername));

        public string DisplayName
        {
            get => GetValue(DisplayNameProperty);
            set => SetValue(DisplayNameProperty, value);
        }

        public string Role
        {
            get => GetValue(RoleProperty);
            set => SetValue(RoleProperty, value);
        }

        public string GitHubUsername
        {
            get => GetValue(GitHubUsernameProperty);
            set => SetValue(GitHubUsernameProperty, value);
        }

        public PersonCard()
        {
            InitializeComponent();
            
            // Bind properties to UI elements
            NameText.Text = DisplayName;
            RoleText.Text = Role;
            
            // Load avatar when GitHubUsername is set
            this.GetObservable(GitHubUsernameProperty).Subscribe(async username =>
            {
                await LoadGitHubAvatarAsync(username);
            });
            
            // Also load avatar when DisplayName is set (for fallback cases)
            this.GetObservable(DisplayNameProperty).Subscribe(async displayName =>
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    // Check if we already have a GitHub username
                    var currentUsername = this.GetValue(GitHubUsernameProperty);
                    if (string.IsNullOrEmpty(currentUsername))
                    {
                        await LoadGitHubAvatarAsync(""); // Force fallback
                    }
                }
            });
        }

        private async Task LoadGitHubAvatarAsync(string username)
        {
            try
            {
                // Hide image and show fallback initially
                AvatarImage.IsVisible = false;
                FallbackAvatar.IsVisible = true;

                // If username is empty, use UI Avatars API as fallback
                if (string.IsNullOrEmpty(username))
                {
                    // Ensure DisplayName is not null before escaping
                    var displayName = DisplayName ?? "Unknown";
                    var fallbackUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(displayName)}&background=random&format=png&size=96";
                    
                    var bitmap = await LoadImageFromUrlAsync(fallbackUrl);
                    if (bitmap != null)
                    {
                        AvatarImage.Source = bitmap;
                        AvatarImage.IsVisible = true;
                        FallbackAvatar.IsVisible = false;
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"[PersonCard] Failed to load UI Avatars for: {displayName}");
                    }
                }
                else
                {
                    // Try to get user info from GitHub API
                    var userInfo = await GetGitHubUserInfoAsync(username);
                    
                    if (userInfo?.AvatarUrl != null)
                    {
                        // Load avatar image
                        var bitmap = await LoadImageFromUrlAsync(userInfo.AvatarUrl);
                        if (bitmap != null)
                        {
                            AvatarImage.Source = bitmap;
                            AvatarImage.IsVisible = true;
                            FallbackAvatar.IsVisible = false;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PersonCard] Error loading avatar for {DisplayName ?? "Unknown"}: {ex.Message}");
            }
            finally
            {
                // Show fallback if no avatar loaded
                if (!AvatarImage.IsVisible)
                {
                    FallbackAvatar.IsVisible = true;
                }
            }
        }

        private async Task<GitHubUserInfo?> GetGitHubUserInfoAsync(string username)
        {
            try
            {
                var url = $"https://api.github.com/search/users?q={username}+in%3Ausername";
                var response = await _httpClient.GetStringAsync(url);
                var searchResult = JsonSerializer.Deserialize<GitHubSearchResult>(response, _jsonOptions);
                
                // Find the user with exact username match
                return searchResult?.Items?.FirstOrDefault(u => u.Login?.Equals(username, StringComparison.OrdinalIgnoreCase) == true);
            }
            catch
            {
                return null;
            }
        }

        private async Task<Bitmap?> LoadImageFromUrlAsync(string url)
        {
            try
            {

                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    
                    // Try to create bitmap with explicit platform
                    var bitmap = new Bitmap(stream);
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PersonCard] Error loading image: {ex.Message}");
            }
            return null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DisplayNameProperty)
            {
                NameText.Text = DisplayName;
            }
            else if (change.Property == RoleProperty)
            {
                RoleText.Text = Role;
            }
        }
    }


}
