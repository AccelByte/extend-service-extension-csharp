// Copyright (c) 2023-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

package common

import (
	"net/http"
	"strings"

	"github.com/rs/cors"
	"github.com/sirupsen/logrus"
)

// CORSConfig holds the cross-origin resource sharing settings for the
// gRPC-Gateway HTTP entry point. All fields map to a CORS_* environment
// variable (see LoadCORSConfigFromEnv). CORS is disabled unless Enabled is
// true, so existing deployments are unaffected until a customer opts in.
type CORSConfig struct {
	Enabled          bool
	AllowedOrigins   []string
	AllowedMethods   []string
	AllowedHeaders   []string
	ExposeHeaders    []string
	AllowCredentials bool
	MaxAge           int
}

// Active reports whether the middleware will actually apply CORS headers.
// Enabled alone is not sufficient: an empty AllowedOrigins list is treated as
// a misconfiguration and leaves CORS off, because rs/cors would otherwise
// interpret it as "allow every origin". See NewCORSMiddleware.
func (c CORSConfig) Active() bool {
	return c.Enabled && len(c.AllowedOrigins) > 0
}

// LoadCORSConfigFromEnv reads the CORS configuration from environment
// variables. CORS is off by default: unless CORS_ENABLED=true the returned
// config is inert and NewCORSMiddleware becomes a passthrough.
func LoadCORSConfigFromEnv() CORSConfig {
	return CORSConfig{
		Enabled:          strings.EqualFold(GetEnv("CORS_ENABLED", "false"), "true"),
		AllowedOrigins:   splitAndTrim(GetEnv("CORS_ALLOWED_ORIGINS", "")),
		AllowedMethods:   splitAndTrim(GetEnv("CORS_ALLOWED_METHODS", "GET,POST,PUT,DELETE,PATCH,OPTIONS")),
		AllowedHeaders:   splitAndTrim(GetEnv("CORS_ALLOWED_HEADERS", "Content-Type,Authorization")),
		ExposeHeaders:    splitAndTrim(GetEnv("CORS_EXPOSE_HEADERS", "")),
		AllowCredentials: strings.EqualFold(GetEnv("CORS_ALLOW_CREDENTIALS", "false"), "true"),
		MaxAge:           GetEnvInt("CORS_MAX_AGE", 600),
	}
}

// NewCORSMiddleware returns an HTTP middleware that applies the given CORS
// config. When cfg.Enabled is false it returns an identity middleware that
// adds no headers and forwards every request untouched. When enabled it
// delegates to rs/cors, which correctly answers preflight OPTIONS requests
// (short-circuiting before the gateway mux) and sets the Vary header.
//
// As a guardrail it logs a warning when credentials are combined with a
// wildcard origin — an invalid combination per the CORS spec that browsers
// reject, and the most common misconfiguration.
func NewCORSMiddleware(cfg CORSConfig, logger *logrus.Logger) func(http.Handler) http.Handler {
	passthrough := func(next http.Handler) http.Handler { return next }

	if !cfg.Enabled {
		return passthrough
	}

	// rs/cors treats an empty AllowedOrigins list as "allow every origin".
	// Arriving there by omission is a misconfiguration rather than an intent,
	// so refuse to enable CORS instead of silently exposing the API to any
	// origin. Allowing everything stays possible, but only explicitly via
	// CORS_ALLOWED_ORIGINS=*.
	if len(cfg.AllowedOrigins) == 0 {
		logger.Error("CORS_ENABLED=true but CORS_ALLOWED_ORIGINS is empty; leaving CORS disabled because an empty origin list would allow every origin. Set explicit origins (e.g. https://mygame.com), or '*' to allow all intentionally")

		return passthrough
	}

	if cfg.AllowCredentials && containsWildcard(cfg.AllowedOrigins) {
		logger.Warn("insecure CORS configuration: CORS_ALLOW_CREDENTIALS=true combined with a wildcard origin '*' is invalid per the CORS spec and will be rejected by browsers; specify exact origins (e.g. https://mygame.com) instead")
	}

	c := cors.New(cors.Options{
		AllowedOrigins:   cfg.AllowedOrigins,
		AllowedMethods:   cfg.AllowedMethods,
		AllowedHeaders:   cfg.AllowedHeaders,
		ExposedHeaders:   cfg.ExposeHeaders,
		AllowCredentials: cfg.AllowCredentials,
		MaxAge:           cfg.MaxAge,
	})

	return c.Handler
}

// splitAndTrim splits a comma-separated list, trims surrounding whitespace
// from each item, and drops empty entries. An empty input yields an empty
// slice rather than a slice containing one empty string.
func splitAndTrim(s string) []string {
	if strings.TrimSpace(s) == "" {
		return []string{}
	}

	parts := strings.Split(s, ",")
	result := make([]string, 0, len(parts))
	for _, p := range parts {
		if trimmed := strings.TrimSpace(p); trimmed != "" {
			result = append(result, trimmed)
		}
	}

	return result
}

func containsWildcard(origins []string) bool {
	for _, o := range origins {
		if o == "*" {
			return true
		}
	}

	return false
}
