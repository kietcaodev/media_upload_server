import { useEffect, useState } from 'react';
import { Drawer, Timeline, Typography, Descriptions, Tag, Spin, Empty } from 'antd';
import { jobsApi } from '../api/services';
import { formatLocalTime } from '../api/client';
import type { JobDto, JobEventDto } from '../types';

const { Text } = Typography;

// Tên hiển thị + màu cho từng bước (khớp JobEventSteps ở backend).
const STEPS: Record<string, { label: string; color: string }> = {
  Received:          { label: 'Nhận file (lưu tạm staging)', color: 'blue' },
  WaitingForWindow:  { label: 'Chờ Time Window', color: 'gray' },
  MovedToNas:        { label: 'Chuyển lên NAS', color: 'blue' },
  Processing:        { label: 'Gửi lên ERP', color: 'blue' },
  ErpSuccess:        { label: 'ERP nhận thành công', color: 'green' },
  ErpFailed:         { label: 'ERP lỗi', color: 'red' },
  RetryScheduled:    { label: 'Hẹn thử lại', color: 'orange' },
  GaveUp:            { label: 'Thất bại (hết lượt thử)', color: 'red' },
  Cancelled:         { label: 'Đã huỷ', color: 'gray' },
  ManualRetry:       { label: 'Retry thủ công', color: 'orange' },
  ResetAfterRestart: { label: 'Reset sau khi khởi động lại', color: 'orange' },
};

/** "+850ms" / "+2.5s" / "+3m 12s" / "+1h 05m" – khoảng cách so với mốc trước. */
function formatDelta(ms: number): string {
  if (ms < 1000) return `+${ms}ms`;
  const s = ms / 1000;
  if (s < 60) return `+${s.toFixed(1)}s`;
  const m = Math.floor(s / 60);
  if (m < 60) return `+${m}m ${String(Math.floor(s % 60)).padStart(2, '0')}s`;
  return `+${Math.floor(m / 60)}h ${String(m % 60).padStart(2, '0')}m`;
}

export default function JobTimelineDrawer({ job, onClose }: { job: JobDto | null; onClose: () => void }) {
  const [events, setEvents] = useState<JobEventDto[]>([]);
  const [loading, setLoading] = useState(false);

  // Tải lại mỗi khi job đổi trạng thái (danh sách job được SignalR làm mới realtime).
  useEffect(() => {
    if (!job) return;
    setLoading(true);
    jobsApi.events(job.id).then(setEvents).finally(() => setLoading(false));
  }, [job?.id, job?.status, job?.retryCount]);

  const first = events.length ? Date.parse(events[0].atUtc) : 0;
  const last = events.length ? Date.parse(events[events.length - 1].atUtc) : 0;

  return (
    <Drawer open={!!job} onClose={onClose} size={560} title={job?.originalFileName} destroyOnHidden>
      {job && (
        <>
          <Descriptions size="small" column={1} bordered style={{ marginBottom: 24 }}>
            <Descriptions.Item label="Trạng thái"><Tag>{job.status}</Tag></Descriptions.Item>
            <Descriptions.Item label="ERP">{job.erpTarget}</Descriptions.Item>
            <Descriptions.Item label="Dung lượng">{(job.fileSize / 1024 / 1024).toFixed(2)} MB</Descriptions.Item>
            <Descriptions.Item label="Số lần thử">{job.retryCount}/{job.maxRetry}</Descriptions.Item>
            {events.length > 1 && (
              <Descriptions.Item label="Tổng thời gian">{formatDelta(last - first).slice(1)}</Descriptions.Item>
            )}
          </Descriptions>

          <Spin spinning={loading}>
            {events.length === 0 && !loading ? <Empty description="Chưa có mốc thời gian" /> : (
              <Timeline
                mode="start"
                titleSpan="160px"
                items={events.map((e, i) => {
                  const step = STEPS[e.step] ?? { label: e.step, color: 'gray' };
                  const at = Date.parse(e.atUtc);
                  return {
                    key: i,
                    color: step.color,
                    title: (
                      <div>
                        <div>{formatLocalTime(e.atUtc, 'DD/MM/YYYY HH:mm:ss.SSS')}</div>
                        {i > 0 && <Text type="secondary" style={{ fontSize: 12 }}>{formatDelta(at - Date.parse(events[i - 1].atUtc))}</Text>}
                      </div>
                    ),
                    content: (
                      <div>
                        <Text strong>{step.label}</Text>
                        {e.detail && <div><Text type="secondary" style={{ wordBreak: 'break-all' }}>{e.detail}</Text></div>}
                      </div>
                    ),
                  };
                })}
              />
            )}
          </Spin>
        </>
      )}
    </Drawer>
  );
}
