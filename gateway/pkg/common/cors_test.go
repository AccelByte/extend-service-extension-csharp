// Copyright (c) 2023-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

package common

import (
	"bytes"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/sirupsen/logrus"
)

// okHandler is the downstream handler the CORS middleware wraps. It records
// whether it was reached so tests can assert preflight short-circuiting.
func okHandler(reached *bool) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if reached != nil {
			*reached = true
		}
		w.WriteHeader(http.StatusOK)
		_, _ = w.Write([]byte("ok"))
	})
}

// newTestLogger returns a logrus logger that writes to the provided buffer at
// warn level, so tests can assert on emitted warnings.
func newTestLogger(buf *bytes.Buffer) *logrus.Logger {
	logger := logrus.New()
	logger.SetOutput(buf)
	logger.SetLevel(logrus.WarnLevel)

	return logger
}

func TestLoadCORSConfigFromEnv_DisabledByDefault(t *testing.T) {
	cfg := LoadCORSConfigFromEnv()

	if cfg.Enabled {
		t.Fatalf("expected CORS to be disabled by default, got Enabled=true")
	}
}

func TestLoadCORSConfigFromEnv_Defaults(t *testing.T) {
	t.Setenv("CORS_ENABLED", "true")

	cfg := LoadCORSConfigFromEnv()

	if !cfg.Enabled {
		t.Fatalf("expected Enabled=true")
	}
	if len(cfg.AllowedOrigins) != 0 {
		t.Errorf("expected no default origins, got %v", cfg.AllowedOrigins)
	}
	if got, want := strings.Join(cfg.AllowedMethods, ","), "GET,POST,PUT,DELETE,PATCH,OPTIONS"; got != want {
		t.Errorf("default methods = %q, want %q", got, want)
	}
	if got, want := strings.Join(cfg.AllowedHeaders, ","), "Content-Type,Authorization"; got != want {
		t.Errorf("default headers = %q, want %q", got, want)
	}
	if cfg.AllowCredentials {
		t.Errorf("expected AllowCredentials=false by default")
	}
	if cfg.MaxAge != 600 {
		t.Errorf("default MaxAge = %d, want 600", cfg.MaxAge)
	}
}

func TestLoadCORSConfigFromEnv_ParsesValues(t *testing.T) {
	t.Setenv("CORS_ENABLED", "true")
	t.Setenv("CORS_ALLOWED_ORIGINS", "https://a.com, https://b.com")
	t.Setenv("CORS_ALLOWED_METHODS", "GET,POST")
	t.Setenv("CORS_ALLOWED_HEADERS", "X-Custom")
	t.Setenv("CORS_EXPOSE_HEADERS", "X-Total-Count")
	t.Setenv("CORS_ALLOW_CREDENTIALS", "true")
	t.Setenv("CORS_MAX_AGE", "120")

	cfg := LoadCORSConfigFromEnv()

	if got, want := strings.Join(cfg.AllowedOrigins, ","), "https://a.com,https://b.com"; got != want {
		t.Errorf("origins = %q, want %q (whitespace should be trimmed)", got, want)
	}
	if got, want := strings.Join(cfg.ExposeHeaders, ","), "X-Total-Count"; got != want {
		t.Errorf("expose = %q, want %q", got, want)
	}
	if !cfg.AllowCredentials {
		t.Errorf("expected AllowCredentials=true")
	}
	if cfg.MaxAge != 120 {
		t.Errorf("MaxAge = %d, want 120", cfg.MaxAge)
	}
}

func TestCORSMiddleware_DisabledIsPassthrough(t *testing.T) {
	cfg := CORSConfig{Enabled: false}
	reached := false
	handler := NewCORSMiddleware(cfg, logrus.New())(okHandler(&reached))

	req := httptest.NewRequest(http.MethodGet, "/v1/thing", nil)
	req.Header.Set("Origin", "https://example.com")
	rec := httptest.NewRecorder()

	handler.ServeHTTP(rec, req)

	if !reached {
		t.Errorf("expected request to reach downstream handler when CORS disabled")
	}
	if got := rec.Header().Get("Access-Control-Allow-Origin"); got != "" {
		t.Errorf("expected no CORS headers when disabled, got Allow-Origin=%q", got)
	}
}

func TestCORSMiddleware_PreflightAllowedOrigin(t *testing.T) {
	cfg := CORSConfig{
		Enabled:        true,
		AllowedOrigins: []string{"https://mygame.com"},
		AllowedMethods: []string{"GET", "POST", "OPTIONS"},
		AllowedHeaders: []string{"Content-Type", "Authorization"},
		MaxAge:         600,
	}
	reached := false
	handler := NewCORSMiddleware(cfg, logrus.New())(okHandler(&reached))

	req := httptest.NewRequest(http.MethodOptions, "/v1/thing", nil)
	req.Header.Set("Origin", "https://mygame.com")
	req.Header.Set("Access-Control-Request-Method", "GET")
	req.Header.Set("Access-Control-Request-Headers", "authorization,content-type")
	rec := httptest.NewRecorder()

	handler.ServeHTTP(rec, req)

	if got := rec.Header().Get("Access-Control-Allow-Origin"); got != "https://mygame.com" {
		t.Errorf("Allow-Origin = %q, want https://mygame.com", got)
	}
	if rec.Code != http.StatusNoContent && rec.Code != http.StatusOK {
		t.Errorf("preflight status = %d, want 204/200", rec.Code)
	}
	if reached {
		t.Errorf("preflight OPTIONS must be short-circuited, not passed to downstream handler")
	}
}

func TestCORSMiddleware_DisallowedOriginGetsNoHeader(t *testing.T) {
	cfg := CORSConfig{
		Enabled:        true,
		AllowedOrigins: []string{"https://mygame.com"},
		AllowedMethods: []string{"GET", "OPTIONS"},
	}
	handler := NewCORSMiddleware(cfg, logrus.New())(okHandler(nil))

	req := httptest.NewRequest(http.MethodGet, "/v1/thing", nil)
	req.Header.Set("Origin", "https://evil.com")
	rec := httptest.NewRecorder()

	handler.ServeHTTP(rec, req)

	if got := rec.Header().Get("Access-Control-Allow-Origin"); got != "" {
		t.Errorf("disallowed origin should get no Allow-Origin header, got %q", got)
	}
}

func TestNewCORSMiddleware_WarnsOnCredentialsWithWildcard(t *testing.T) {
	var buf bytes.Buffer
	logger := newTestLogger(&buf)

	cfg := CORSConfig{
		Enabled:          true,
		AllowedOrigins:   []string{"*"},
		AllowCredentials: true,
	}
	_ = NewCORSMiddleware(cfg, logger)

	logs := buf.String()
	if !strings.Contains(strings.ToLower(logs), "credential") || !strings.Contains(logs, "*") {
		t.Errorf("expected a warning about credentials + wildcard origin, got logs: %q", logs)
	}
}

func TestNewCORSMiddleware_NoWarnOnExactOriginWithCredentials(t *testing.T) {
	var buf bytes.Buffer
	logger := newTestLogger(&buf)

	cfg := CORSConfig{
		Enabled:          true,
		AllowedOrigins:   []string{"https://mygame.com"},
		AllowCredentials: true,
	}
	_ = NewCORSMiddleware(cfg, logger)

	if buf.Len() != 0 {
		t.Errorf("expected no warning for exact origin + credentials, got logs: %q", buf.String())
	}
}

func TestNewCORSMiddleware_EmptyOriginsDoesNotAllowEveryOrigin(t *testing.T) {
	var buf bytes.Buffer
	logger := newTestLogger(&buf)

	cfg := CORSConfig{Enabled: true, AllowedOrigins: []string{}}
	reached := false
	handler := NewCORSMiddleware(cfg, logger)(okHandler(&reached))

	req := httptest.NewRequest(http.MethodGet, "/v1/task", nil)
	req.Header.Set("Origin", "https://evil.com")
	rec := httptest.NewRecorder()

	handler.ServeHTTP(rec, req)

	if got := rec.Header().Get("Access-Control-Allow-Origin"); got != "" {
		t.Errorf("empty origin list must not emit Allow-Origin, got %q", got)
	}
	if !reached {
		t.Errorf("request must still reach the downstream handler")
	}
	if !strings.Contains(buf.String(), "CORS_ALLOWED_ORIGINS is empty") {
		t.Errorf("expected an error log about the empty origin list, got: %q", buf.String())
	}
}

func TestNewCORSMiddleware_ExplicitWildcardStillAllowsAll(t *testing.T) {
	cfg := CORSConfig{
		Enabled:        true,
		AllowedOrigins: []string{"*"},
		AllowedMethods: []string{"GET", "OPTIONS"},
	}
	handler := NewCORSMiddleware(cfg, logrus.New())(okHandler(nil))

	req := httptest.NewRequest(http.MethodGet, "/v1/task", nil)
	req.Header.Set("Origin", "https://anything.com")
	rec := httptest.NewRecorder()

	handler.ServeHTTP(rec, req)

	if got := rec.Header().Get("Access-Control-Allow-Origin"); got != "*" {
		t.Errorf("explicit '*' must still allow all origins, got Allow-Origin=%q", got)
	}
}

func TestCORSConfig_Active(t *testing.T) {
	cases := []struct {
		name string
		cfg  CORSConfig
		want bool
	}{
		{"disabled", CORSConfig{Enabled: false}, false},
		{"disabled with origins", CORSConfig{Enabled: false, AllowedOrigins: []string{"https://a.com"}}, false},
		{"enabled but no origins", CORSConfig{Enabled: true, AllowedOrigins: []string{}}, false},
		{"enabled with origins", CORSConfig{Enabled: true, AllowedOrigins: []string{"https://a.com"}}, true},
		{"enabled with wildcard", CORSConfig{Enabled: true, AllowedOrigins: []string{"*"}}, true},
	}

	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			if got := tc.cfg.Active(); got != tc.want {
				t.Errorf("Active() = %v, want %v", got, tc.want)
			}
		})
	}
}

// identityHandler is a pointer-typed handler so tests can compare handler
// identity with ==, which http.HandlerFunc (a func type) does not permit.
type identityHandler struct{}

func (identityHandler) ServeHTTP(http.ResponseWriter, *http.Request) {}

// TestNewCORSMiddleware_DisabledReturnsTheSameHandler is the strongest
// guarantee that CORS_ENABLED=false changes nothing: the middleware hands
// back the exact handler it was given, so the served chain is byte-for-byte
// the one that ran before CORS was introduced.
func TestNewCORSMiddleware_DisabledReturnsTheSameHandler(t *testing.T) {
	next := &identityHandler{}

	for _, cfg := range []CORSConfig{
		{Enabled: false},
		{Enabled: false, AllowedOrigins: []string{"https://a.com"}, AllowCredentials: true},
		{Enabled: true, AllowedOrigins: []string{}}, // misconfigured -> also inert
	} {
		got := NewCORSMiddleware(cfg, newTestLogger(&bytes.Buffer{}))(next)
		if got != http.Handler(next) {
			t.Errorf("cfg %+v: middleware wrapped the handler; want the identical instance", cfg)
		}
	}
}
