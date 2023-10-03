package main

import (
	"flag"
	"fmt"
	"log"
	"os"
	"os/signal"
	"net/http"
	"path/filepath"
	_ "net/http/pprof"
	common "extend-grpc-gateway/pkg/common"

	_ "golang.org/x/net/trace"

	"github.com/sirupsen/logrus"
	"github.com/golang/glog"
	"golang.org/x/net/context"
)

var (
	grpcAddr = flag.String("grpc-addr", "localhost:6565", "listen grpc address")
	gatewayHttpPort = flag.Int("http-port", 8000, "listen http port")
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

	return &http.Server{
		Addr:     addr,
		Handler:  mux,
		ErrorLog: log.New(os.Stderr, "httpSrv: ", log.LstdFlags), // Configure the logger for the HTTP server
	}
}

func serveSwaggerUI(mux *http.ServeMux) {
	swaggerUIDir := "third_party/swagger-ui"
	fileServer := http.FileServer(http.Dir(swaggerUIDir))
	swaggerUiPath := fmt.Sprintf("%s/apidocs/", common.BasePath)
	mux.Handle(swaggerUiPath, http.StripPrefix(swaggerUiPath, fileServer))
}

func serveSwaggerJSON(mux *http.ServeMux, swaggerDir string) {
	fileHandler := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		matchingFiles, err := filepath.Glob(filepath.Join(swaggerDir, "*.swagger.json"))
		if err != nil || len(matchingFiles) == 0 {
			http.Error(w, "Error finding Swagger JSON file", http.StatusInternalServerError)

			return
		}

		firstMatchingFile := matchingFiles[0]
		http.ServeFile(w, r, firstMatchingFile)
	})
	apidocsPath := fmt.Sprintf("%s/apidocs/api.json", common.BasePath)
	mux.Handle(apidocsPath, fileHandler)
}

func main() {
	flag.Parse()
	defer glog.Flush()
	
	logrus.Infof("starting gateway server..")

	ctx := context.Background()
	ctx, cancel := context.WithCancel(ctx)
	defer cancel()

	// Create a new HTTP server for the gRPC-Gateway
	grpcGateway, err := common.NewGateway(ctx, *grpcAddr)
	if err != nil {
		logrus.Fatalf("Failed to create gRPC-Gateway: %v", err)
	}

	// Start the gRPC-Gateway HTTP server
	go func() {
		swaggerDir := "apidocs" // Path to swagger directory
		grpcGatewayHTTPServer := newGRPCGatewayHTTPServer(fmt.Sprintf(":%d", *gatewayHttpPort), grpcGateway, logrus.New(), swaggerDir)
		logrus.Infof("Starting gRPC-Gateway HTTP server on port %d", *gatewayHttpPort)
		if err := grpcGatewayHTTPServer.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			logrus.Fatalf("Failed to run gRPC-Gateway HTTP server: %v", err)
		}
	}()

	logrus.Infof("grpc server started")

	ctx, _ = signal.NotifyContext(ctx, os.Interrupt)
	<-ctx.Done()
}
