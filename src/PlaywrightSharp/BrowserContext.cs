using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlaywrightSharp
{
    /// <inheritdoc cref="IBrowserContext"/>
    public class BrowserContext : IBrowserContext
    {
        private readonly IBrowserContextDelegate _delegate;
        private bool _closed;

        internal BrowserContext(IBrowserContextDelegate browserContextDelegate, BrowserContextOptions options = null)
        {
            _delegate = browserContextDelegate;
            _delegate.BrowserContext = this;
            Options = options?.Clone() ?? new BrowserContextOptions();

            if (Options.Geolocation != null)
            {
                VerifyGeolocation(Options.Geolocation);
            }

            Options.Viewport ??= new Viewport
            {
                Width = 800,
                Height = 600,
            };
        }

        /// <inheritdoc cref="IBrowserContext.Options"/>
        public BrowserContextOptions Options { get; }

        /// <inheritdoc cref="IBrowserContext.ClearCookiesAsync"/>
        public Task ClearCookiesAsync() => _delegate.ClearCookiesAsync();

        /// <inheritdoc cref="IBrowserContext.CloseAsync"/>
        public async Task CloseAsync()
        {
            if (_closed)
            {
                return;
            }

            await _delegate.CloseAsync().ConfigureAwait(false);
            _closed = true;
        }

        /// <inheritdoc cref="IBrowserContext.GetCookiesAsync(string[])"/>
        public async Task<IEnumerable<NetworkCookie>> GetCookiesAsync(params string[] urls)
            => FilterCookies(await _delegate.GetCookiesAsync().ConfigureAwait(false), urls);

        /// <inheritdoc cref="IBrowserContext.GetPagesAsync"/>
        public Task<IPage[]> GetPagesAsync() => _delegate.GetPagesAsync();

        /// <inheritdoc cref="IBrowserContext.NewPageAsync(string)"/>
        public async Task<IPage> NewPageAsync(string url = null)
        {
            var page = await _delegate.NewPageAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(url))
            {
                await page.GoToAsync(url).ConfigureAwait(false);
            }

            return page;
        }

        /// <inheritdoc cref="IBrowserContext.SetCookiesAsync(SetNetworkCookieParam[])"/>
        public Task SetCookiesAsync(params SetNetworkCookieParam[] cookies)
            => _delegate.SetCookiesAsync(RewriteCookies(cookies));

        /// <inheritdoc cref="IBrowserContext.SetGeolocationAsync(GeolocationOption)"/>
        public Task SetGeolocationAsync(GeolocationOption geolocation = null)
        {
            if (geolocation != null)
            {
                VerifyGeolocation(geolocation);
            }

            Options.Geolocation = geolocation;
            return _delegate.SetGeolocationAsync(geolocation);
        }

        /// <inheritdoc cref="IBrowserContext.ClearPermissionsAsync"/>
        public Task ClearPermissionsAsync() => _delegate.ClearPermissionsAsync();

        /// <inheritdoc cref="IBrowserContext.GetExistingPages"/>
        public IEnumerable<IPage> GetExistingPages() => _delegate.GetExistingPages();

        /// <inheritdoc cref="IBrowserContext.SetPermissionsAsync(string, ContextPermission[])"/>
        public Task SetPermissionsAsync(string origin, params ContextPermission[] permissions)
            => _delegate.SetPermissionsAsync(origin, permissions);

        internal async Task InitializeAsync()
        {
            if (Options?.Permissions?.Count > 0)
            {
                await Task.WhenAll(Options.Permissions.Select(permission => SetPermissionsAsync(permission.Key, permission.Value)).ToArray()).ConfigureAwait(false);
            }

            if (Options?.Geolocation != null)
            {
                await SetGeolocationAsync(Options.Geolocation).ConfigureAwait(false);
            }
        }

        private static SetNetworkCookieParam[] RewriteCookies(SetNetworkCookieParam[] cookies)
        {
            return Array.ConvertAll(cookies, c =>
            {
                if (string.IsNullOrEmpty(c.Name))
                {
                    throw new ArgumentException("Cookie should have a name");
                }

                if (string.IsNullOrEmpty(c.Value))
                {
                    throw new ArgumentException("Cookie should have a value");
                }

                if (string.IsNullOrEmpty(c.Url) && (string.IsNullOrEmpty(c.Domain) || string.IsNullOrEmpty(c.Path)))
                {
                    throw new ArgumentException("Cookie should have a url or a domain/path pair");
                }

                if (!string.IsNullOrEmpty(c.Url) && !string.IsNullOrEmpty(c.Domain))
                {
                    throw new ArgumentException("Cookie should have either url or domain");
                }

                if (!string.IsNullOrEmpty(c.Url) && !string.IsNullOrEmpty(c.Path))
                {
                    throw new ArgumentException("Cookie should have either url or domain");
                }

                var copy = c.Clone();
                if (!string.IsNullOrEmpty(copy.Url))
                {
                    if (copy.Url == "about:blank")
                    {
                        throw new ArgumentException($"Blank page can not have cookie \"{c.Name}\"");
                    }

                    if (copy.Url.StartsWith("data:"))
                    {
                        throw new ArgumentException($"Data URL page can not have cookie \"{c.Name}\"");
                    }

                    var url = new Uri(copy.Url);
                    copy.Domain = url.Host;
                    copy.Path = url.AbsolutePath.Substring(0, url.AbsolutePath.LastIndexOf('/') + 1);
                    copy.Secure = url.Scheme == "https";
                }

                return copy;
            });
        }

        private static IEnumerable<NetworkCookie> FilterCookies(IEnumerable<NetworkCookie> cookies, string[] urls)
        {
            var parsedUrls = Array.ConvertAll(urls, url => new Uri(url));
            return cookies.Where(c =>
            {
                if (urls.Length == 0)
                {
                    return true;
                }

                foreach (var parsedUrl in parsedUrls)
                {
                    if (parsedUrl.Host != c.Domain)
                    {
                        continue;
                    }

                    if (!parsedUrl.AbsolutePath.StartsWith(c.Path))
                    {
                        continue;
                    }

                    if ((parsedUrl.Scheme == "https") != c.Secure)
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            });
        }

        private void VerifyGeolocation(GeolocationOption geolocation)
        {
            if (geolocation.Longitude < -180 || geolocation.Longitude > 180)
            {
                throw new ArgumentException($"Invalid longitude '{geolocation.Longitude}': precondition -180 <= LONGITUDE <= 180 failed.");
            }

            if (geolocation.Latitude < -90 || geolocation.Latitude > 90)
            {
                throw new ArgumentException($"Invalid latitude '{geolocation.Latitude}': precondition -90 <= LONGITUDE <= 90 failed.");
            }

            if (geolocation.Accuracy < 0)
            {
                throw new ArgumentException($"Invalid accuracy '{geolocation.Accuracy}': precondition 0 <= LONGITUDE failed.");
            }
        }
    }
}
