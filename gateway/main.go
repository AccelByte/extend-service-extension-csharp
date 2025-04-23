// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

package main

import (
	"bytes"
	"encoding/json"
	common "extend-grpc-gateway/pkg/common"
	"flag"
	"fmt"
	"log"
	"net/http"
	_ "net/http/pprof"
	"os"
	"os/signal"
	"path/filepath"
	"time"

	_ "golang.org/x/net/trace"

	"github.com/go-openapi/loads"
	"github.com/golang/glog"
	"github.com/sirupsen/logrus"
	"golang.org/x/net/context"
	"syscall"
)

var (
	grpcAddr			= flag.String("grpc-addr", "localhost:6565", "listen grpc address")
	gatewayHttpPort		= flag.Int("http-port", 8000, "listen http port")
	serviceName         = common.GetEnv("OTEL_SERVICE_NAME", "ExtendCustomServiceGrpcGateway")
	basePath			= common.GetBasePath()
)

func newGRPCGatewayHTTPServer(
	addr string, handler http.Handler, logger *logrus.Logger, swaggerDir string,
) *http.Server {
	// Create a new ServeMux
	mux := http.NewServeMux()

	// Add the gRPC-Gateway handler
	mux.Handle("/", handler)

	// Serve Swagger UI and JSON
	serveSwaggerUI(mux)
	serveSwaggerJSON(mux, swaggerDir)

	loggedMux := loggingMiddleware(logger, mux)

	return &http.Server{
		Addr:     addr,
		Handler:  loggedMux,
		ErrorLog: log.New(os.Stderr, "httpSrv: ", log.LstdFlags), // Configure the logger for the HTTP server
	}
}

func loggingMiddleware(logger *logrus.Logger, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		next.ServeHTTP(w, r)
		duration := time.Since(start)
		logger.WithFields(logrus.Fields{
			"method":   r.Method,
			"path":     r.URL.Path,
			"duration": duration,
		}).Info("HTTP request")
	})
}

func serveSwaggerUI(mux *http.ServeMux) {
	swaggerUIDir := "third_party/swagger-ui"
	fileServer := http.FileServer(http.Dir(swaggerUIDir))
	swaggerUiPath := fmt.Sprintf("%s/apidocs/", basePath)
	mux.Handle(swaggerUiPath, http.StripPrefix(swaggerUiPath, fileServer))
}

func serveSwaggerJSON(mux *http.ServeMux, swaggerDir string) {
	fileHandler := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		swagger, err := loads.Spec(filepath.Join(swaggerDir, "service.swagger.json"))
		if err != nil {
			http.Error(w, "Error parsing Swagger JSON file", http.StatusInternalServerError)
			return
		}

		// Update the base path
		swagger.Spec().BasePath = basePath

		updatedSwagger, err := swagger.Spec().MarshalJSON()
		if err != nil {
			http.Error(w, "Error serializing updated Swagger JSON", http.StatusInternalServerError)
			return
		}
		var prettySwagger bytes.Buffer
		err = json.Indent(&prettySwagger, updatedSwagger, "", "  ")
		if err != nil {
			http.Error(w, "Error formatting updated Swagger JSON", http.StatusInternalServerError)
			return
		}

		_, err = w.Write(prettySwagger.Bytes())
		if err != nil {
			http.Error(w, "Error writing Swagger JSON response", http.StatusInternalServerError)
			return
		}
	})
	apidocsPath := fmt.Sprintf("%s/apidocs/api.json", basePath)
	mux.Handle(apidocsPath, fileHandler)
}

func main() {
	flag.Parse()
	defer glog.Flush()	
	
	logrus.Infof("Starting %s on port %d with base path %s", serviceName, *gatewayHttpPort, basePath)

	ctx := context.Background()
	ctx, cancel := context.WithCancel(ctx)
	defer cancel()

	stopTracing := common.InitTracing(ctx, serviceName)
	defer stopTracing()

	// Create a new HTTP server for the gRPC-Gateway
	grpcGateway, err := common.NewGateway(ctx, *grpcAddr, basePath)
	if err != nil {
		logrus.Fatalf("Failed to create gRPC-Gateway: %v", err)
	}

	// Start the gRPC-Gateway HTTP server
	go func() {
		swaggerDir := "apidocs" // Path to swagger directory
		grpcGatewayHTTPServer := newGRPCGatewayHTTPServer(fmt.Sprintf(":%d", *gatewayHttpPort), grpcGateway, logrus.New(), swaggerDir)
		if err := grpcGatewayHTTPServer.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			logrus.Fatalf("Failed to run gRPC-Gateway HTTP server: %v", err)
		}		
	}()

	logrus.Infof("%s started", serviceName)

	ctx, stop := signal.NotifyContext(ctx, os.Interrupt, syscall.SIGTERM)
	defer stop()
	<-ctx.Done()
	logrus.Infof("SIGTERM received")
}
