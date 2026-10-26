package main

import (
	"context"
	"errors"
	"flag"
	"log"
	"net"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"syscall"
	"time"

	"inspection/backend/internal/api"
	"inspection/backend/internal/issue"
)

func main() {
	addr := flag.String("addr", envOr("ADDR", ":8080"), "监听地址；:8080 表示监听所有网卡，手机可通过局域网 IP 访问")
	dataFile := flag.String("data", envOr("DATA_FILE", "data/issues.json"), "JSON 数据文件路径")
	corsOrigin := flag.String("cors-origin", envOr("CORS_ORIGIN", "*"), "允许跨域访问的来源")
	flag.Parse()

	logger := log.New(os.Stdout, "", log.LstdFlags)

	store, err := issue.OpenStore(*dataFile)
	if err != nil {
		logger.Fatal(err)
	}
	absPath, _ := filepath.Abs(store.Path())
	logger.Printf("数据文件: %s（已加载 %d 条问题）", absPath, store.Count())

	srv := &http.Server{
		Addr:              *addr,
		Handler:           api.NewHandler(store, *corsOrigin, logger),
		ReadHeaderTimeout: 5 * time.Second,
	}

	// Ctrl+C 时优雅退出：等待处理中的请求完成。
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()
	go func() {
		<-ctx.Done()
		shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		_ = srv.Shutdown(shutdownCtx)
	}()

	logger.Printf("服务已启动: http://localhost%s", portSuffix(*addr))
	for _, u := range lanURLs(*addr) {
		logger.Printf("Unity 真机可使用: %s", u)
	}
	if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		logger.Fatal(err)
	}
	logger.Println("服务已停止")
}

func envOr(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func portSuffix(addr string) string {
	if _, port, err := net.SplitHostPort(addr); err == nil {
		return ":" + port
	}
	return addr
}

// lanURLs 列出本机各网卡的局域网 IPv4 地址（附网卡名，便于区分 WLAN 和虚拟网卡），方便填写到手机端。
// 只监听 127.0.0.1 等具体地址时，手机无法访问，不列出。
func lanURLs(addr string) []string {
	if host, _, err := net.SplitHostPort(addr); err != nil || (host != "" && host != "0.0.0.0") {
		return nil
	}
	ifaces, err := net.Interfaces()
	if err != nil {
		return nil
	}
	var urls []string
	for _, iface := range ifaces {
		if iface.Flags&net.FlagUp == 0 || iface.Flags&net.FlagLoopback != 0 {
			continue
		}
		addrs, err := iface.Addrs()
		if err != nil {
			continue
		}
		for _, a := range addrs {
			ipNet, ok := a.(*net.IPNet)
			if !ok {
				continue
			}
			if ip := ipNet.IP.To4(); ip != nil && ip.IsPrivate() {
				urls = append(urls, "http://"+ip.String()+portSuffix(addr)+"  ("+iface.Name+")")
			}
		}
	}
	return urls
}
