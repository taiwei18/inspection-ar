// Package api 把 issue.Store 暴露为 HTTP JSON 接口，供 Unity 客户端和 React 管理端共同使用。
package api

import (
	"encoding/json"
	"errors"
	"log"
	"net/http"
	"time"

	"inspection/backend/internal/issue"
)

const maxBodyBytes = 1 << 20

// NewHandler 注册全部路由（依赖 Go 1.22 的 "METHOD /path/{param}" 路由语法），并套上 CORS 与访问日志。
func NewHandler(store *issue.Store, allowedOrigin string, logger *log.Logger) http.Handler {
	h := &handlers{store: store, logger: logger}

	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/health", h.health)
	mux.HandleFunc("GET /api/issues", h.list)
	mux.HandleFunc("POST /api/issues", h.create)
	mux.HandleFunc("GET /api/issues/{id}", h.get)
	mux.HandleFunc("PATCH /api/issues/{id}", h.updateStatus)

	return logRequests(logger, cors(allowedOrigin, mux))
}

type handlers struct {
	store  *issue.Store
	logger *log.Logger
}

func (h *handlers) health(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{
		"status":     "ok",
		"time":       time.Now().UTC().Format(time.RFC3339),
		"issueCount": h.store.Count(),
	})
}

type listResponse struct {
	Items []issue.Issue `json:"items"`
}

func (h *handlers) list(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, listResponse{Items: h.store.List()})
}

func (h *handlers) get(w http.ResponseWriter, r *http.Request) {
	it, err := h.store.Get(r.PathValue("id"))
	if err != nil {
		h.writeStoreError(w, err)
		return
	}
	writeJSON(w, http.StatusOK, it)
}

func (h *handlers) create(w http.ResponseWriter, r *http.Request) {
	var in issue.CreateInput
	if !decodeJSON(w, r, &in) {
		return
	}
	created, err := h.store.Create(in)
	if err != nil {
		h.writeStoreError(w, err)
		return
	}
	writeJSON(w, http.StatusCreated, created)
}

type statusInput struct {
	Status issue.Status `json:"status"`
}

func (h *handlers) updateStatus(w http.ResponseWriter, r *http.Request) {
	var in statusInput
	if !decodeJSON(w, r, &in) {
		return
	}
	updated, err := h.store.UpdateStatus(r.PathValue("id"), in.Status)
	if err != nil {
		h.writeStoreError(w, err)
		return
	}
	writeJSON(w, http.StatusOK, updated)
}

// ---- 响应与错误格式 ----

// 所有错误统一为 {"error": {"code": "...", "message": "...", "field": "..."}}。
type errorBody struct {
	Error apiError `json:"error"`
}

type apiError struct {
	Code    string `json:"code"`
	Message string `json:"message"`
	Field   string `json:"field,omitempty"`
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}

func writeError(w http.ResponseWriter, status int, code, message, field string) {
	writeJSON(w, status, errorBody{apiError{Code: code, Message: message, Field: field}})
}

func (h *handlers) writeStoreError(w http.ResponseWriter, err error) {
	var ve *issue.ValidationError
	switch {
	case errors.As(err, &ve):
		writeError(w, http.StatusBadRequest, "invalid_argument", ve.Message, ve.Field)
	case errors.Is(err, issue.ErrNotFound):
		writeError(w, http.StatusNotFound, "not_found", "问题不存在", "id")
	default:
		h.logger.Printf("internal error: %v", err)
		writeError(w, http.StatusInternalServerError, "internal", "服务器内部错误", "")
	}
}

// decodeJSON 解析请求体，失败时直接写出 400 并返回 false。
func decodeJSON(w http.ResponseWriter, r *http.Request, dst any) bool {
	r.Body = http.MaxBytesReader(w, r.Body, maxBodyBytes)
	err := json.NewDecoder(r.Body).Decode(dst)
	if err == nil {
		return true
	}
	// 字段类型错误（例如 priority 传了数字）时指出具体字段。
	var typeErr *json.UnmarshalTypeError
	if errors.As(err, &typeErr) {
		writeError(w, http.StatusBadRequest, "invalid_argument", typeErr.Field+" 的类型不正确，应为 "+typeErr.Type.Kind().String(), typeErr.Field)
		return false
	}
	writeError(w, http.StatusBadRequest, "invalid_json", "请求体不是合法的 JSON: "+err.Error(), "")
	return false
}

// ---- 中间件 ----

// cors 允许浏览器直接跨域访问（Vite 开发代理之外的另一种方式）。
func cors(allowedOrigin string, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", allowedOrigin)
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PATCH, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(w, r)
	})
}

type statusRecorder struct {
	http.ResponseWriter
	status int
}

func (r *statusRecorder) WriteHeader(code int) {
	r.status = code
	r.ResponseWriter.WriteHeader(code)
}

// logRequests 打印每个请求的方法、路径、状态码、耗时和来源 IP，便于确认手机请求是否到达。
func logRequests(logger *log.Logger, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		rec := &statusRecorder{ResponseWriter: w, status: http.StatusOK}
		next.ServeHTTP(rec, r)
		logger.Printf("%s %s %d %s from %s", r.Method, r.URL.Path, rec.status, time.Since(start).Round(time.Millisecond), r.RemoteAddr)
	})
}
