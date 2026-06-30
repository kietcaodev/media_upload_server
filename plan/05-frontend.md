# Frontend – React 18 + Ant Design 5

> **Stack:** React 18, TypeScript, Ant Design 5, Zustand, Axios, @microsoft/signalr, Vite

---

## Quy Tắc Timezone

> **Nguyên tắc nhất quán xuyên suốt toàn bộ frontend:**

| Tình huống | Giá trị sử dụng |
|------------|------------------|
| **Hiển thị cho người dùng** | GMT+7 (Asia/Ho_Chi_Minh) |
| **Gửi lên API** | ISO 8601 UTC (`2026-06-30T07:00:00Z`) |
| **Nhận từ API** | ISO 8601 UTC → convert sang GMT+7 trước hiển thị |
| **TimeWindow TimePicker** | Người dùng chọn giờ GMT+7, gửi dưới dạng `"HH:mm"` string |
| **Date columns trong Table** | Hiển thị GMT+7 dùng `formatLocalTime()` |

---

## Cài Đặt

```bash
npm create vite@latest media-upload-ui -- --template react-ts
cd media-upload-ui
npm install antd @ant-design/icons @ant-design/charts
npm install @microsoft/signalr axios zustand
npm install react-router-dom
npm install dayjs dayjs-plugin-timezone  # timezone handling
```

### Setup dayjs (main.tsx)

```typescript
// src/main.tsx
import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import timezone from "dayjs/plugin/timezone";

dayjs.extend(utc);
dayjs.extend(timezone);

// Timezone đọc từ API settings khi khởi động app
// settingsStore.fetchSettings() → set APP_TZ
export let APP_TZ = "Asia/Ho_Chi_Minh"; // fallback mặc định
export const setAppTimezone = (tz: string) => { APP_TZ = tz; };

// Helper dùng khắp nơi — không bao giờ hardcode "Asia/Ho_Chi_Minh" trực tiếp
export const formatLocalTime = (utcString: string, format = "HH:mm DD/MM/YYYY") =>
  dayjs(utcString).tz(APP_TZ).format(format);

export const toUtcIso = (localDatetime: dayjs.Dayjs) =>
  localDatetime.tz(APP_TZ).utc().toISOString();
```

---

## Layout Tổng Thể

```
┌─────────────────────────────────────────────────────────────┐
│  HEADER: Logo | Nav: Dashboard | Config | Upload            │
├─────────────────────────────────────────────────────────────┤
│  WORKER STATUS BAR                                          │
│  🟢 Worker đang chạy | Trong khung giờ "Tối" (đến 22:00)  │
│  [⏸ Tạm Dừng Worker]                    Active Jobs: 3     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   [NỘI DUNG TRANG]                                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Trang 1: Dashboard (`/dashboard`)

### Layout

```
┌──────────────────────────────────────────────────────────────┐
│  THỐNG KÊ NHANH                                              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐       │
│  │ PENDING  │ │ RUNNING  │ │  DONE    │ │  FAILED  │       │
│  │   12     │ │    3     │ │   450    │ │    2     │       │
│  │ 🟡       │ │ 🔵       │ │  🟢      │ │  🔴      │       │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘       │
├──────────────────────────────────────────────────────────────┤
│  BIỂU ĐỒ TIẾN TRÌNH (Bar Chart)                             │
│  [Hôm nay ▼] [Tuần này] [Tháng này]  [ERP: Tất cả ▼]       │
│  ▓▓▓▓░░░░░░░▓▓▓▓▓▓░░░░░░░░░▓▓▓▓▓▓▓▓                        │
├──────────────────────────────────────────────────────────────┤
│  DANH SÁCH JOBS                                              │
│  [🔍 Tìm kiếm...] [Status▼] [ERP▼] [Từ ngày] [Đến ngày]   │
│                                                              │
│  FileId | Tên file | ERP | Status | Retry | Thời gian | ... │
│  ──────────────────────────────────────────────────────────  │
│  b239e3  video.mp4  DND  🟡 Pending  0/3  10:00 30/06  ...  │
│  651f24  audio.mp3 Zomz  🟢 Done     0/3  09:45 30/06  ...  │
│  108625  video2.mp4 Zozin 🔴 Failed  3/3  09:00 30/06 [↺]  │
│                                    [Pause][Resume][Cancel]   │
│  ──────────────────────────────────────────────────────────  │
│  < 1 2 3 ... 23 >                                           │
└──────────────────────────────────────────────────────────────┘
```

### StatsCards Component

```tsx
// pages/Dashboard/StatsCards.tsx
import { Card, Col, Row, Statistic } from "antd";
import { ClockCircleOutlined, SyncOutlined, CheckCircleOutlined, CloseCircleOutlined } from "@ant-design/icons";

const statusConfig = [
  { key: "pending",  label: "Đang Chờ",  color: "#faad14", icon: <ClockCircleOutlined /> },
  { key: "running",  label: "Đang Chạy", color: "#1677ff", icon: <SyncOutlined spin /> },
  { key: "done",     label: "Thành Công", color: "#52c41a", icon: <CheckCircleOutlined /> },
  { key: "failed",   label: "Thất Bại",  color: "#ff4d4f", icon: <CloseCircleOutlined /> },
];

export function StatsCards({ stats }) {
  return (
    <Row gutter={16}>
      {statusConfig.map(({ key, label, color, icon }) => (
        <Col span={6} key={key}>
          <Card>
            <Statistic
              title={label}
              value={stats[key]}
              valueStyle={{ color }}
              prefix={icon}
            />
          </Card>
        </Col>
      ))}
    </Row>
  );
}
```

### JobsTable Component

```tsx
// pages/Dashboard/JobsTable.tsx
import { Table, Tag, Button, Space, Tooltip, Input } from "antd";
import { useJobStore } from "../../store/jobStore";

const statusColors = {
  Pending: "orange", Running: "blue", Done: "green",
  Failed: "red", Paused: "purple", Cancelled: "default"
};

export function JobsTable() {
  const { jobs, total, loading, fetchJobs, pauseJob, resumeJob, retryJob, cancelJob } = useJobStore();

  const columns = [
    { title: "File ID", dataIndex: "fileId", width: 120, render: (v) => <code>{v.slice(0, 8)}</code> },
    { title: "Tên File", dataIndex: "originalName", ellipsis: true },
    { title: "ERP", dataIndex: "erpTarget", width: 80 },
    {
      title: "Trạng Thái", dataIndex: "status", width: 120,
      render: (status) => <Tag color={statusColors[status]}>{status}</Tag>
    },
    {
      title: "Retry", dataIndex: "retryCount", width: 70,
      render: (count, row) => `${count}/${row.maxRetry}`
    },
    { title: "Tạo lúc", dataIndex: "createdAt",
      // Luôn dùng formatLocalTime — không dùng new Date().toLocaleString() trực tiếp
      render: (v) => formatLocalTime(v) },
    {
      title: "Thao tác", width: 200,
      render: (_, row) => (
        <Space>
          {row.status === "Pending" && (
            <Tooltip title="Tạm dừng">
              <Button size="small" onClick={() => pauseJob(row.id)}>⏸</Button>
            </Tooltip>
          )}
          {row.status === "Paused" && (
            <Tooltip title="Tiếp tục">
              <Button size="small" type="primary" onClick={() => resumeJob(row.id)}>▶</Button>
            </Tooltip>
          )}
          {row.status === "Failed" && (
            <Tooltip title="Thử lại">
              <Button size="small" type="primary" danger onClick={() => retryJob(row.id)}>↺</Button>
            </Tooltip>
          )}
          {["Pending", "Paused"].includes(row.status) && (
            <Tooltip title="Hủy">
              <Button size="small" danger onClick={() => cancelJob(row.id)}>✕</Button>
            </Tooltip>
          )}
        </Space>
      )
    }
  ];

  return (
    <Table
      columns={columns}
      dataSource={jobs}
      rowKey="id"
      loading={loading}
      pagination={{ total, pageSize: 20, onChange: (page) => fetchJobs({ page }) }}
    />
  );
}
```

---

## Trang 2: Config (`/config`)

> **4 Tabs:** Khung Giờ | ERP Endpoints | Credentials | Cài Đặt Hệ Thống

### Tab 1: Khung Giờ

```
┌──────────────────────────────────────────────────────────────┐
│  CẤU HÌNH KHUNG GIỜ PUSH ERP          [+ Thêm Khung Giờ]    │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ 🟢 Sáng sớm (Mon–Fri 07:00–08:00)  ERP: Tất cả      │   │
│  │    Đồng thời: 5 jobs    [Sửa] [Xóa]                  │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ 🟢 Tối (Mon–Fri 18:00–22:00)  ERP: Tất cả           │   │
│  │    Đồng thời: 10 jobs   [Sửa] [Xóa]                  │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  CALENDAR PREVIEW – Trực quan khung giờ trong tuần          │
│  ┌──── T2 ─────┬──── T3 ─────┬── ... ──┬──── T6 ────┐     │
│  │ 07:00 ░░░░░ │ 07:00 ░░░░░ │         │ 07:00 ░░░░ │     │
│  │ 08:00       │ 08:00       │         │ 08:00       │     │
│  │ ...         │ ...         │         │ ...         │     │
│  │ 18:00 ░░░░░ │ 18:00 ░░░░░ │         │ 18:00 ░░░░ │     │
│  │ 22:00       │ 22:00       │         │ 22:00       │     │
│  └─────────────┴─────────────┴─────────┴────────────-┘     │
└──────────────────────────────────────────────────────────────┘
```

### TimeWindowForm Component (Modal)

```tsx
// pages/Config/TimeWindowForm.tsx
import { Modal, Form, Input, TimePicker, Select, InputNumber, Switch, Checkbox } from "antd";
import dayjs from "dayjs";

const DAYS = [
  { label: "T2", value: 1 }, { label: "T3", value: 2 }, { label: "T4", value: 3 },
  { label: "T5", value: 4 }, { label: "T6", value: 5 },
  { label: "T7", value: 6 }, { label: "CN", value: 7 },
];

export function TimeWindowForm({ open, initial, onSave, onClose }) {
  const [form] = Form.useForm();

  const handleOk = async () => {
    const values = await form.validateFields();
    await onSave({
      ...values,
      startTime: values.timeRange[0].format("HH:mm"),
      endTime: values.timeRange[1].format("HH:mm"),
      daysOfWeek: values.days.join(","),
    });
    onClose();
  };

  return (
    <Modal title={initial ? "Sửa Khung Giờ" : "Thêm Khung Giờ"}
      open={open} onOk={handleOk} onCancel={onClose}>
      <Form form={form} layout="vertical" initialValues={initial}>
        <Form.Item name="name" label="Tên khung giờ" rules={[{ required: true }]}>
          <Input placeholder="VD: Ngoài giờ hành chính – Sáng" />
        </Form.Item>
        <Form.Item name="days" label="Ngày trong tuần" rules={[{ required: true }]}>
          <Checkbox.Group options={DAYS} />
        </Form.Item>
        <Form.Item name="timeRange" label="Khung giờ" rules={[{ required: true }]}>
          <TimePicker.RangePicker format="HH:mm" minuteStep={15} />
        </Form.Item>
        <Form.Item name="maxConcurrent" label="Số job đồng thời tối đa">
          <InputNumber min={1} max={20} defaultValue={5} />
        </Form.Item>
        <Form.Item name="erpTarget" label="Áp dụng cho ERP">
          <Select allowClear placeholder="Tất cả" options={[
            { label: "DND", value: "DND" },
            { label: "Zomzem", value: "Zomzem" },
            { label: "Zozin", value: "Zozin" },
          ]} />
        </Form.Item>
        <Form.Item name="isEnabled" label="Kích hoạt" valuePropName="checked">
          <Switch defaultChecked />
        </Form.Item>
      </Form>
    </Modal>
  );
}
```

### Tab 2: ERP Endpoints

```
┌──────────────────────────────────────────────────────────────┐
│  CẤU HÌNH ERP ENDPOINTS                                      │
│                                                              │
│  DND                                          🟢 Bật  [Sửa]  │
│  URL: https://locnuoc365.xyz/api/...                         │
│  Token: ***abc123   Timeout: 30s                             │
│                                                              │
│  Zomzem                                       🟢 Bật  [Sửa]  │
│  URL: https://zomzem.xyz/api/...                             │
│  Token: ***xyz789   Timeout: 30s                             │
└──────────────────────────────────────────────────────────────┘
```

### Tab 3: Credentials

> Xem chi tiết tại [08-authentication.md](./08-authentication.md)

```
┌──────────────────────────────────────────────────────────────┐
│  QUẢN LÝ CREDENTIALS    [+Bearer] [+Basic] [+API Key]        │
│                                                              │
│  Tên              │ Loại        │ Prefix      │ Quyền  │ ... │
│  ERP DND Client   │ 🔑 Bearer  │ abc12345... │ U R    │ ... │
│  Zomzem Integ.    │ 🔐 Basic   │ zomzem_u..  │ U      │ ... │
│  NAS Scanner      │ 🗝 API Key │ key_abc1... │ U      │ ... │
│  [Bật/Tắt] [Rotate Secret] [Xóa]                            │
└──────────────────────────────────────────────────────────────┘
```

### Tab 4: Cài Đặt Hệ Thống

> Xem chi tiết tại [09-system-settings.md](./09-system-settings.md)

```
┌──────────────────────────────────────────────────────────────┐
│  CẤU HÌNH HỆ THỐNG                         [💾 Lưu Tất Cả]  │
│  ┌─ NAS ──────────────────────────────────────────────────┐  │
│  │ Upload Dir  [/mnt/nas/uploads]  Logs Dir [/mnt/nas/logs]│  │
│  │ Min Disk Space [1] GB                                   │  │
│  └─────────────────────────────────────────────────────────┘  │
│  ┌─ UPLOAD ───────────────────────────────────────────────┐  │
│  │ Max file size [1500] MB   Max files/request [5]         │  │
│  │ Đuôi file: [.mp4][.avi][.mp3][.wav]... [+Thêm]         │  │
│  └─────────────────────────────────────────────────────────┘  │
│  ┌─ WORKER ───────────────────────────────────────────────┐  │
│  │ Tick interval [30]s  Max retry [3]  Retry delay [5]phút│  │
│  └─────────────────────────────────────────────────────────┘  │
│  ┌─ RATE LIMIT ⚠restart ──────────────────────────────────┐  │
│  │ Upload: [20]req/[15]phút   API: [60]req/[1]phút         │  │
│  └─────────────────────────────────────────────────────────┘  │
│  ┌─ CORS ⚠restart ────────────────────────────────────────┐  │
│  │ [http://localhost:5173][✕]  [+Thêm origin]              │  │
│  └─────────────────────────────────────────────────────────┘  │
│              [↺ Reset Mặc Định]        [💾 Lưu Tất Cả]       │
└──────────────────────────────────────────────────────────────┘
```

---

## Trang 3: Upload (`/upload`)

```
┌──────────────────────────────────────────────────────────────┐
│  UPLOAD VIDEO / AUDIO                                        │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                                                      │   │
│  │   📁 Kéo thả file vào đây hoặc click để chọn        │   │
│  │   Hỗ trợ: MP4, AVI, MOV, MP3 | Tối đa 1500MB/file  │   │
│  │                                                      │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  Mã nhân viên: [__________]  Mã KH: [__________]           │
│  Gửi đến ERP:  [DND ▼]                                      │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ video.mp4    14.12 MB   ████████████░░  75%   Uploading│  │
│  │ audio.mp3    0.38 MB    ████████████████ 100% ✅ Done  │  │
│  └──────────────────────────────────────────────────────┘   │
│                                [Upload]                      │
└──────────────────────────────────────────────────────────────┘
```

---

## WorkerStatusBar Component

```tsx
// components/WorkerStatusBar.tsx
import { Alert, Button, Space, Badge, Tag } from "antd";
import { useWorkerStore } from "../store/workerStore";

export function WorkerStatusBar() {
  const { status, pause, resume } = useWorkerStore();

  if (!status) return null;

  return (
    <Alert
      type={status.isPaused ? "warning" : "info"}
      message={
        <Space>
          <Badge status={status.isPaused ? "warning" : "processing"} />
          {status.isPaused
            ? `Worker tạm dừng: ${status.pausedReason || "Thủ công"}`
            : `Worker đang chạy${status.currentWindow ? ` – "${status.currentWindow.name}"` : ""}`}
          <Tag>{status.activeJobsCount} jobs đang xử lý</Tag>
        </Space>
      }
      action={
        <Space>
          {status.isPaused
            ? <Button size="small" type="primary" onClick={resume}>▶ Resume</Button>
            : <Button size="small" onClick={() => pause("Tạm dừng thủ công")}>⏸ Pause</Button>
          }
        </Space>
      }
      banner
    />
  );
}
```

---

## Zustand Stores

> 5 stores: `jobStore`, `workerStore`, `configStore`, `credentialStore`, `settingsStore`

### jobStore.ts

```typescript
// store/jobStore.ts
import { create } from "zustand";
import * as jobService from "../services/jobService";

interface JobStore {
  jobs: Job[];
  total: number;
  loading: boolean;
  filters: JobFilters;
  fetchJobs: (params?: Partial<JobFilters>) => Promise<void>;
  updateJobStatus: (payload: StatusChangedPayload) => void;
  pauseJob: (id: string) => Promise<void>;
  resumeJob: (id: string) => Promise<void>;
  retryJob: (id: string) => Promise<void>;
  cancelJob: (id: string) => Promise<void>;
}

export const useJobStore = create<JobStore>((set, get) => ({
  jobs: [],
  total: 0,
  loading: false,
  filters: { page: 1, pageSize: 20 },

  fetchJobs: async (params) => {
    const filters = { ...get().filters, ...params };
    set({ loading: true, filters });
    const res = await jobService.getJobs(filters);
    set({ jobs: res.data, total: res.total, loading: false });
  },

  updateJobStatus: ({ jobId, status }) => {
    set((state) => ({
      jobs: state.jobs.map((j) =>
        j.id === jobId ? { ...j, status } : j
      )
    }));
  },

  pauseJob: async (id) => {
    await jobService.pauseJob(id);
    await get().fetchJobs();
  },
  resumeJob: async (id) => {
    await jobService.resumeJob(id);
    await get().fetchJobs();
  },
  retryJob: async (id) => {
    await jobService.retryJob(id);
    await get().fetchJobs();
  },
  cancelJob: async (id) => {
    await jobService.cancelJob(id);
    await get().fetchJobs();
  },
}));
```

### signalrService.ts

> `settingsStore.ts` và `credentialStore.ts` — xem chi tiết tại [08-authentication.md](./08-authentication.md) và [09-system-settings.md](./09-system-settings.md).

```typescript
// store/settingsStore.ts
import { create } from "zustand";
import * as settingsService from "../services/settingsService";

export const useSettingsStore = create((set, get) => ({
  settings: {} as Record<string, Record<string, SettingItem>>,
  loading: false,
  fetchSettings: async () => {
    set({ loading: true });
    const data = await settingsService.getSettings();
    set({ settings: data, loading: false });
  },
  saveSettings: async (patches: { key: string; value: string }[]) => {
    const result = await settingsService.patchSettings(patches);
    await get().fetchSettings(); // reload
    return result;
  },
  resetAll: async () => {
    await settingsService.resetAll();
    await get().fetchSettings();
  },
}));
```

### signalrService.ts

```typescript
// services/signalrService.ts
import * as signalR from "@microsoft/signalr";
import { useJobStore } from "../store/jobStore";
import { useWorkerStore } from "../store/workerStore";
import { useDashboardStore } from "../store/dashboardStore";

let connection: signalR.HubConnection;

export function initSignalR(baseUrl: string) {
  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/hubs/jobs`)
    .withAutomaticReconnect([1000, 3000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.on("job:statusChanged", (payload) => {
    useJobStore.getState().updateJobStatus(payload);
  });

  connection.on("job:created", () => {
    useJobStore.getState().fetchJobs();
  });

  connection.on("worker:statusChanged", (payload) => {
    useWorkerStore.getState().setStatus(payload);
  });

  connection.on("stats:updated", (stats) => {
    useDashboardStore.getState().setStats(stats);
  });

  connection.onreconnecting(() => console.warn("SignalR reconnecting..."));
  connection.onreconnected(() => console.info("SignalR reconnected"));

  return connection.start();
}
```

---

## package.json (dependencies)

```json
{
  "dependencies": {
    "antd": "^5.21.0",
    "@ant-design/icons": "^5.5.0",
    "@ant-design/charts": "^2.2.0",
    "@microsoft/signalr": "^8.0.7",
    "axios": "^1.7.0",
    "zustand": "^5.0.0",
    "react-router-dom": "^6.26.0",
    "dayjs": "^1.11.13",
    "dayjs-plugin-timezone": "^1.0.0"
  },
  "devDependencies": {
    "typescript": "^5.5.0",
    "vite": "^6.0.0",
    "@types/react": "^18.3.0"
  }
}
```
