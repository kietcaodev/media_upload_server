import { useEffect } from 'react';
import { Row, Col, Card, Statistic, Button, Table, Tag, Typography, Space, Alert } from 'antd';
import {
  CheckCircleOutlined, CloseCircleOutlined, ClockCircleOutlined,
  LoadingOutlined, PauseCircleOutlined, PlayCircleOutlined, SyncOutlined
} from '@ant-design/icons';
import { useDashboardStore } from '../stores/dashboardStore';
import { useWorkerStore } from '../stores/workerStore';
import { useJobStore } from '../stores/jobStore';
import { formatLocalTime } from '../api/client';
import type { JobDto } from '../types';

const { Title } = Typography;

const statusColor: Record<string, string> = {
  Pending: 'gold', Processing: 'blue', Success: 'green',
  Failed: 'red', Cancelled: 'default', Paused: 'purple',
};

export default function DashboardPage() {
  const { stats, fetchStats, fetchTimeline, startSignalR, stopSignalR } = useDashboardStore();
  const { status: workerStatus, fetch: fetchWorker, pause, resume } = useWorkerStore();
  const { jobs, total, page, pageSize, loading, fetch: fetchJobs } = useJobStore();

  useEffect(() => {
    fetchStats();
    fetchTimeline();
    fetchWorker();
    fetchJobs(1);
    startSignalR();
    return () => stopSignalR();
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
          <Card><Statistic title="Processing" value={stats?.processingJobs ?? '-'} valueStyle={{ color: '#1677ff' }} prefix={<LoadingOutlined />} /></Card>
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
