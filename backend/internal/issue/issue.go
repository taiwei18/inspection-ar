// Package issue 定义巡检问题的数据结构、枚举和校验规则，并提供基于 JSON 文件的持久化存储。
package issue

import (
	"math"
	"strings"
	"time"
	"unicode/utf8"
)

type Priority string

const (
	PriorityLow    Priority = "low"
	PriorityMedium Priority = "medium"
	PriorityHigh   Priority = "high"
)

func (p Priority) Valid() bool {
	switch p {
	case PriorityLow, PriorityMedium, PriorityHigh:
		return true
	}
	return false
}

type Status string

const (
	StatusOpen       Status = "open"
	StatusInProgress Status = "in_progress"
	StatusResolved   Status = "resolved"
)

func (s Status) Valid() bool {
	switch s {
	case StatusOpen, StatusInProgress, StatusResolved:
		return true
	}
	return false
}

// Position 是标记在 AR 会话坐标系中的位置，单位为米。
type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// Issue 的 JSON 字段名与 Unity、React 两端保持一致。
type Issue struct {
	ID          string    `json:"id"`
	Title       string    `json:"title"`
	Description string    `json:"description"`
	Priority    Priority  `json:"priority"`
	Status      Status    `json:"status"`
	Position    Position  `json:"position"`
	CreatedAt   time.Time `json:"createdAt"`
	UpdatedAt   time.Time `json:"updatedAt"`
}

// CreateInput 是创建问题时客户端可以提交的字段；id、status 和时间戳由服务端生成。
type CreateInput struct {
	Title       string    `json:"title"`
	Description string    `json:"description"`
	Priority    Priority  `json:"priority"`
	Position    *Position `json:"position"`
}

// ValidationError 表示某个字段不合法，API 层会把它转换成 400 响应。
type ValidationError struct {
	Field   string
	Message string
}

func (e *ValidationError) Error() string { return e.Field + ": " + e.Message }

const (
	MaxTitleLen       = 100
	MaxDescriptionLen = 1000
)

// Normalize 去掉首尾空白、校验字段，并把坐标统一到毫米精度。
func (in *CreateInput) Normalize() error {
	in.Title = strings.TrimSpace(in.Title)
	in.Description = strings.TrimSpace(in.Description)

	switch {
	case in.Title == "":
		return &ValidationError{"title", "title 不能为空"}
	case utf8.RuneCountInString(in.Title) > MaxTitleLen:
		return &ValidationError{"title", "title 不能超过 100 个字符"}
	case utf8.RuneCountInString(in.Description) > MaxDescriptionLen:
		return &ValidationError{"description", "description 不能超过 1000 个字符"}
	// encoding/json 会把非 UTF-8 字节替换成 U+FFFD，出现它说明客户端用了 GBK 等编码，直接拒绝以免存入乱码。
	case strings.ContainsRune(in.Title, utf8.RuneError):
		return &ValidationError{"title", "title 包含无法识别的字符，请使用 UTF-8 编码提交"}
	case strings.ContainsRune(in.Description, utf8.RuneError):
		return &ValidationError{"description", "description 包含无法识别的字符，请使用 UTF-8 编码提交"}
	case !in.Priority.Valid():
		return &ValidationError{"priority", "priority 必须是 low、medium、high 之一"}
	case in.Position == nil:
		return &ValidationError{"position", "position 必填，格式为 {x, y, z}"}
	}

	// Unity 的 float 序列化后会出现 0.41999998 这类尾数，AR 定位本身也只有厘米级精度，保留 3 位小数即可。
	in.Position = &Position{X: roundMM(in.Position.X), Y: roundMM(in.Position.Y), Z: roundMM(in.Position.Z)}
	return nil
}

func roundMM(v float64) float64 { return math.Round(v*1000) / 1000 }
