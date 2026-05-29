# Facebook Webhook Platform

Hệ thống xử lý webhook Facebook Page theo kiến trúc microservices hướng sự kiện. Facebook gửi comment/message vào `webhook-service`, dữ liệu được chuẩn hóa và đưa vào Kafka, `core-service` xử lý AI/spam/automation, `backend-api` là service duy nhất gọi Facebook Graph API, còn `retry-service` quản lý retry và dead letter queue.

## Kiến trúc hiện tại

![System architecture](./system_architecture.png)

Luồng xử lý chính:

1. Facebook gửi webhook đến `webhook-service` qua endpoint `GET /webhook` để verify và `POST /webhook` để nhận event.
2. `webhook-service` xác thực token, kiểm tra chữ ký `X-Hub-Signature-256`, normalize payload Facebook thành `RawEvent`, rồi publish vào topic `raw_events`.
3. `core-service` consume `raw_events`, dedup theo `eventId`, bỏ qua event do chính page tạo, phát hiện spam, phân loại intent/sentiment bằng AI hoặc heuristic fallback, áp dụng automation rule và publish `reply_commands`.
4. `backend-api` consume `reply_commands` và `send_retry`, kiểm tra idempotency bằng `commandId` trong PostgreSQL, sau đó gọi Facebook Graph API để reply hoặc ẩn comment.
5. Nếu gọi Graph API lỗi, `backend-api` publish `send_failed`.
6. `retry-service` consume `send_failed`, tính backoff theo `baseDelaySeconds * 2^retryCount`, publish lại vào `send_retry` hoặc chuyển sang `dead_letter` khi hết số lần retry.
7. Kafka Exporter, Prometheus và Alertmanager theo dõi offset/alert cho các topic lỗi.

## Service và port

| Service | Project | Port | Vai trò |
| --- | --- | --- | --- |
| `backend-api` | `FB.BackendAPI` | `3000` | REST API quản trị, consume command, kiểm tra idempotency và gọi Facebook Graph API. |
| `webhook-service` | `FB.WebhookService` | `3001` | Nhận webhook Facebook, verify token/chữ ký, normalize event và publish Kafka. |
| `core-service` | `FB.CoreService` | `3002` | Xử lý spam, AI classification, automation rule và publish command. |
| `retry-service` | `FB.RetryService` | `3003` | Retry command lỗi, chuyển message thất bại cuối cùng sang DLQ. |

Health check:

```text
GET http://localhost:3000/health
GET http://localhost:3001/health
GET http://localhost:3002/health
GET http://localhost:3003/health
```

## Kafka topics

| Topic | Producer | Consumer | Nội dung |
| --- | --- | --- | --- |
| `raw_events` | `webhook-service` | `core-service` | Event Facebook đã normalize. |
| `reply_commands` | `core-service` | `backend-api` | Lệnh `reply` hoặc `hide_comment`. |
| `send_failed` | `backend-api` | `retry-service` | Lỗi khi gọi Facebook Graph API. |
| `send_retry` | `retry-service` | `backend-api` | Command được thử lại sau backoff. |
| `dead_letter` | `retry-service` | Không có consumer nghiệp vụ | Message lỗi cuối cùng để vận hành xử lý thủ công. |

Không có topic `manual_review` trong kiến trúc hiện tại. Các trường hợp cần duyệt thủ công được biểu diễn bằng trạng thái xử lý hoặc field `requiresManualReview` trong command.

## Thành phần hạ tầng

`docker-compose.yml` khởi động các thành phần:

- Kafka + Zookeeper
- Kafka topic initializer
- Kafka UI tại `http://localhost:8080`
- Kafka Exporter tại `http://localhost:9308/metrics`
- Prometheus tại `http://localhost:9090`
- Alertmanager tại `http://localhost:9093`
- PostgreSQL tại `localhost:5432`

## Công nghệ

- .NET 8
- ASP.NET Core Web API và BackgroundService
- Apache Kafka
- PostgreSQL
- Facebook Graph API và Facebook Webhooks
- OpenAI-compatible AI endpoint, có thể cấu hình Gemini qua endpoint tương thích
- Heuristic fallback khi AI chưa cấu hình, chậm hoặc lỗi
- Docker Compose, Kafka UI, Prometheus, Alertmanager

## Cấu hình môi trường

Tạo file `.env` từ file mẫu:

```powershell
Copy-Item .env.example .env
```

Các nhóm cấu hình chính:

```env
Kafka__BootstrapServers=localhost:9092
Database__ConnectionString=Host=localhost;Port=5432;Database=...;Username=...;Password=...

FacebookWebhook__VerifyToken=...
FacebookWebhook__AppSecret=...

FacebookGraph__AppId=...
FacebookGraph__DefaultPageId=...
FacebookGraph__GraphVersion=v25.0
FacebookGraph__PageAccessToken=...
FacebookGraph__TimeoutSeconds=30

DashboardAuth__AdminToken=...

AiClassification__Provider=fallback
AiClassification__Endpoint=...
AiClassification__ApiKey=...
AiClassification__Model=...
AiClassification__TimeoutSeconds=15

RetryProcessing__MaxRetries=3
RetryProcessing__BaseDelaySeconds=1
```

Ghi chú:

- `AiClassification__Provider=fallback` sẽ không gọi AI ngoài, hệ thống dùng heuristic classifier.
- Muốn gọi AI ngoài, đặt `AiClassification__Provider=openai-compatible` và cấu hình `Endpoint`, `ApiKey`, `Model`.
- Không commit file `.env` thật vì chứa token và secret.

## PostgreSQL

Hệ thống sử dụng PostgreSQL cho:

- Bảng `comments`: lưu/truy vết trạng thái comment.
- Bảng `idempotency_keys`: lưu `command_id` đã xử lý thành công để tránh reply/ẩn comment trùng khi Kafka redeliver message.

`backend-api` kiểm tra `idempotency_keys` trước khi gọi Facebook Graph API. Nếu `commandId` đã có `status = 'processed'`, command trùng sẽ bị bỏ qua và không tạo tác dụng phụ lần hai trên Facebook.

## Chạy local

Khởi động hạ tầng:

```powershell
docker compose up -d
```

Build solution:

```powershell
dotnet build FacebookWebhook.slnx
```

Chạy các service ở 4 terminal riêng:

```powershell
dotnet run --project FB.WebhookService --launch-profile http
dotnet run --project FB.CoreService --launch-profile http
dotnet run --project FB.BackendAPI --launch-profile http
dotnet run --project FB.RetryService --launch-profile http
```

Swagger/local UI:

| Thành phần | URL |
| --- | --- |
| Backend API Swagger | `http://localhost:3000/swagger` |
| Webhook Service Swagger | `http://localhost:3001/swagger` |
| Core Service Swagger | `http://localhost:3002/swagger` |
| Retry Service Swagger | `http://localhost:3003/swagger` |
| Kafka UI | `http://localhost:8080` |
| Prometheus | `http://localhost:9090` |
| Alertmanager | `http://localhost:9093` |

## Cấu hình Facebook Webhook

Khi chạy local, cần đưa `webhook-service` ra internet. Ví dụ dùng ngrok:

```powershell
ngrok http http://127.0.0.1:3001
```

Trong Meta Developer:

- Callback URL: `https://<ngrok-domain>/webhook`
- Verify Token: giá trị của `FacebookWebhook__VerifyToken`
- App Secret: giá trị của `FacebookWebhook__AppSecret` dùng để verify `X-Hub-Signature-256`

## Backend Admin API

Các endpoint quản trị nằm ở `backend-api` và yêu cầu header:

```text
X-Admin-Token: <DashboardAuth__AdminToken>
```

Endpoint chính:

- `GET /api/facebook/posts`: lấy danh sách bài viết của page.
- `POST /api/facebook/posts`: đăng bài mới lên page.
- `POST /api/facebook/comments/{commentId}/reply`: reply comment thủ công.
- `POST /api/facebook/comments/{commentId}/hide`: ẩn/hiện comment.
- `GET /api/command-status/{commandId}`: xem trạng thái command trong runtime store.

`core-service` expose:

- `GET /api/events/{eventId}`: xem trạng thái xử lý event trong runtime store.

## Reliability

Các cơ chế đã có trong code:

- Idempotency cho command: `backend-api` dedup theo `commandId` trong PostgreSQL.
- Dedup event: `core-service` dedup theo `eventId` trong memory.
- Manual Kafka commit: consumer commit offset sau khi xử lý message.
- Retry có giới hạn: `retry-service` dùng exponential backoff và `MaxRetries`.
- Dead Letter Queue: message hết retry hoặc không retry được đi vào `dead_letter`.
- Circuit breaker: backend bảo vệ Facebook Graph API, core bảo vệ AI classifier.
- Fallback AI: nếu AI chưa cấu hình hoặc lỗi, hệ thống dùng heuristic classification.

## Monitoring và vận hành

Prometheus đọc metric từ Kafka Exporter. Alertmanager nhận alert từ Prometheus và có thể gửi cảnh báo qua Slack bằng `SLACK_WEBHOOK_URL`.

Các tình huống cần theo dõi:

- Có message mới ở `send_failed`.
- Có message retry ở `send_retry`.
- Có message ở `dead_letter`.
- Consumer lag tăng bất thường.

Kafka UI dùng để kiểm tra topic, payload message, offset và consumer group khi cần debug hoặc xử lý thủ công.

## Kịch bản kiểm thử nhanh

1. Gửi comment hỏi giá trên Facebook Page.
   - Kỳ vọng: `webhook-service` publish `raw_events`, `core-service` tạo `reply_commands`, `backend-api` reply comment.
2. Gửi comment tích cực.
   - Kỳ vọng: hệ thống phân loại `positive` và reply cảm ơn.
3. Gửi comment tiêu cực hoặc yêu cầu hỗ trợ.
   - Kỳ vọng: hệ thống phân loại `negative/support_request` và reply xin lỗi/hỗ trợ.
4. Gửi comment có link hoặc nội dung spam.
   - Kỳ vọng: `core-service` tạo command `hide_comment`, `backend-api` gọi Graph API để ẩn comment.
5. Publish lại cùng một `reply_commands` với cùng `commandId`.
   - Kỳ vọng: `backend-api` log `Skipped duplicate command`, bảng `idempotency_keys` giữ `status = processed`, Facebook không bị reply trùng.
6. Cấu hình sai token Facebook để test lỗi.
   - Kỳ vọng: message đi qua `send_failed`, retry hoặc `dead_letter` tùy loại lỗi; Prometheus/Alertmanager phát hiện.
