import { useEffect } from 'react';
import { Row, Col, Card, Statistic, Button, Table, Tag, Typography, Space, Alert, Progress } from 'antd';
import {
  CheckCircleOutlined, CloseCircleOutlined, ClockCircleOutlined, HddOutlined,
  PauseCircleOutlined, PlayCircleOutlined, SyncOutlined
} from '@ant-design/icons';
import { useDashboardStore } from '../stores/dashboardStore';
import { useWorkerStore } from '../stores/workerStore';
import { useJobStore } from '../stores/jobStore';
import { formatLocalTime } from '../api/client';
import type { JobDto } from '../types';

const { Title } = Typography;

function formatBytes(b: number): string {
  const gb = b / 1024 ** 3;
  return gb >= 1024 ? `${(gb / 1024).toFixed(2)} TB` : `${gb.toFixed(1)} GB`;
}

const statusColor: Record<string, string> = {
  Pending: 'gold', Processing: 'blue', Success: 'green',
  Failed: 'red', Cancelled: 'default', Paused: 'purple',
};

export default function DashboardPage() {
  const { stats, fetchStats, fetchTimeline } = useDashboardStore();
  const { status: workerStatus, fetch: fetchWorker, pause, resume } = useWorkerStore();
  const { jobs, total, page, pageSize, loading, fetch: fetchJobs } = useJobStore();

  useEffect(() => {
    fetchStats();
    fetchTimeline();
    fetchWorker();
    fetchJobs(1);
  }, []);

  const columns = [
    { title: 'File', dataIndex: 'originalFileName', key: 'file', ellipsis: true },
    { title: 'ERP', dataIndex: 'erpTarget', key: 'erp', width: 90 },
    {
      title: 'Status', dataIndex: 'status', key: 'status', width: 110,
      render: (s: string) => <Tag color={statusColor[s] || 'default'}>{s}</Tag>
    },
    { title: 'Retry', dataIndex: 'retryCount', key: 'retry', width: 70,
      render: (v: number, r: JobDto) => `${v}/${r.maxRetry}` },
    {
      title: 'Created', dataIndex: 'createdAtUtc', key: 'created', width: 160,
      render: (v: string) => formatLocalTime(v)
    },
    {
      title: 'Lỗi', dataIndex: 'lastError', key: 'error', ellipsis: true,
      render: (v: string) => v ? <Typography.Text type="danger" ellipsis>{v}</Typography.Text> : '-'
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Row justify="space-between" align="middle">
        <Col><Title level={3} style={{ margin: 0 }}>Dashboard</Title></Col>
        <Col>
          <Space>
            <Button icon={<SyncOutlined />} onClick={() => { fetchStats(); fetchJobs(page); }}>
              Refresh
            </Button>
            {workerStatus?.isPaused ? (
              <Button type="primary" icon={<PlayCircleOutlined />} onClick={() => resume()}>
                Resume Worker
              </Button>
            ) : (
              <Button danger icon={<PauseCircleOutlined />} onClick={() => pause('Paused by admin')}>
                Pause Worker
              </Button>
            )}
          </Space>
        </Col>
      </Row>

      {workerStatus?.isPaused && (
        <Alert
          type="warning"
          message={`Worker đang tạm dừng: ${workerStatus.pauseReason || 'N/A'}`}
          showIcon
        />
      )}

      {stats && !stats.withinTimeWindow && !workerStatus?.isPaused && (
        <Alert type="info" message="Ngoài time window – worker sẽ không xử lý job cho đến khi vào window." showIcon />
      )}

      {/* Stats cards */}
      <Row gutter={16}>
        <Col xs={12} sm={8} lg={4}>
          <Card><Statistic title="Tổng" value={stats?.totalJobs ?? '-'} /></Card>
        </Col>
        <Col xs={12} sm={8} lg={4}>
          <Card><Statistic title="Pending" value={stats?.pendingJobs ?? '-'} valueStyle={{ color: '#faad14' }} prefix={<ClockCircleOutlined />} /></Card>
        </Col>
        <Col xs={12} sm={8} lg={4}>
          <Card><Statistic title="Processing" value={stats?.processingJobs ?? '-'} valueStyle={{ color: '#1677ff' }} prefix={<SyncOutlined spin={!!stats?.processingJobs} />} /></Card>
        </Col>
        <Col xs={12} sm={8} lg={4}>
          <Card><Statistic title="Success" value={stats?.successJobs ?? '-'} valueStyle={{ color: '#52c41a' }} prefix={<CheckCircleOutlined />} /></Card>
        </Col>
        <Col xs={12} sm={8} lg={4}>
          <Card><Statistic title="Failed" value={stats?.failedJobs ?? '-'} valueStyle={{ color: '#ff4d4f' }} prefix={<CloseCircleOutlined />} /></Card>
        </Col>
        <Col xs={12} sm={8} lg={4}>
          <Card>
            <Statistic
              title="Worker"
              value={workerStatus?.isPaused ? 'Paused' : `Active (${stats?.activeWorkers ?? 0})`}
              valueStyle={{ color: workerStatus?.isPaused ? '#ff4d4f' : '#52c41a' }}
            />
          </Card>
        </Col>
      </Row>

      {stats?.disks.filter(d => d.freeBytes < d.minFreeBytes).map(d => (
        <Alert key={d.path} type="error" showIcon
          message={`${d.label} sắp đầy: còn ${formatBytes(d.freeBytes)}, dưới ngưỡng ${formatBytes(d.minFreeBytes)} – upload đang bị từ chối`} />
      ))}

      {/* Disk usage */}
      <Row gutter={16}>
        {stats?.disks.map(d => {
          const used = d.totalBytes - d.freeBytes;
          const percent = d.totalBytes ? Math.round((used / d.totalBytes) * 100) : 0;
          // Đỏ: còn < 2× ngưỡng tối thiểu hoặc ≥ 90%; cam: ≥ 80%.
          const color = d.freeBytes < d.minFreeBytes * 2 || percent >= 90 ? '#ff4d4f' : percent >= 80 ? '#faad14' : '#52c41a';
          return (
            <Col xs={24} md={12} key={d.path}>
              <Card size="small" title={<><HddOutlined /> {d.label}</>}>
                <Progress percent={percent} strokeColor={color} />
                <Typography.Text>
                  Còn trống <b style={{ color }}>{formatBytes(d.freeBytes)}</b> / {formatBytes(d.totalBytes)}
                </Typography.Text>
                <br />
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {d.path} (mount {d.mountPoint}) · chặn upload khi còn dưới {formatBytes(d.minFreeBytes)}
                </Typography.Text>
              </Card>
            </Col>
          );
        })}
      </Row>

      {/* Jobs table */}
      <Card title="Jobs gần nhất">
        <Table
          dataSource={jobs}
          columns={columns}
          rowKey="id"
          loading={loading}
          size="small"
          pagination={{
            total,
            current: page,
            pageSize,
            onChange: (p) => fetchJobs(p),
            showTotal: (t) => `${t} jobs`,
          }}
          scroll={{ x: 900 }}
        />
      </Card>
    </Space>
  );
}
