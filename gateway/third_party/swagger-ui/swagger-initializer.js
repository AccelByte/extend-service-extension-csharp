window.onload = function () {
    let url = window.location.search.match(/url=([^&]+)/);
    if (url && url.length > 1) {
        url = decodeURIComponent(url[1]);
    } else {
        url = window.location.pathname.replace(/apidocs.*/, 'apidocs/api.json');
    }
    let service = window.location.pathname.match(/([^/]+)/)[0];
    if (service) {
        document.title = service.charAt(0).toUpperCase() + service.slice(1) + " APIs";
        document.getElementById("logo").innerText = service.charAt(0).toUpperCase() + service.slice(1) + " APIs";
    }
    window.ui = SwaggerUIBundle({
        url: url,
        dom_id: '#swagger-ui',
        deepLinking: true,
        docExpansion: 'none',
        showCommonExtensions: true,
        showExtensions: false,
        presets: [
            SwaggerUIBundle.presets.apis,
            SwaggerUIStandalonePreset
        ],
        plugins: [
            SwaggerUIBundle.plugins.DownloadUrl
        ],
        layout: "BaseLayout"
    });
};
