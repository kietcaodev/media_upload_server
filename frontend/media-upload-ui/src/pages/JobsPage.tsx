import { useEffect } from 'react';
import { Table, Tag, Space, Button, Select, Typography, Card, Popconfirm } from 'antd';
import { RedoOutlined, StopOutlined, SyncOutlined } from '@ant-design/icons';
import { useJobStore } from '../stores/jobStore';
import { formatLocalTime } from '../api/client';
import type { JobDto } from '../types';

const { Title } = Typography;

const statusColor: Record<string, string> = {
  Pending: 'gold', Processing: 'blue', Success: 'green',
  Failed: 'red', Cancelled: 'default', Paused: 'purple',
};

const STATUS_OPTIONS = [
  { value: '', label: 'Tất cả' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Processing', label: 'Processing' },
  { value: 'Success', label: 'Success' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export default function JobsPage() {
  const { jobs, total, page, pageSize, loading, statusFilter, fetch, cancel, retry, setFilter } = useJobStore();

  useEffect(() => { fetch(1); }, []);

  const columns = [
    { title: 'File', dataIndex: 'originalFileName', ellipsis: true },
    { title: 'ERP', dataIndex: 'erpTarget', width: 90 },
    { title: 'Size', dataIndex: 'fileSize', width: 90, render: (v: number) => `${(v / 1024 / 1024).toFixed(1)} MB` },
    { title: 'Status', dataIndex: 'status', width: 110, render: (s: string) => <Tag color={statusColor[s]}>{s}</Tag> },
    { title: 'Retry', key: 'retry', width: 70, render: (_: any, r: JobDto) => `${r.retryCount}/${r.maxRetry}` },
    { title: 'Tạo lúc', dataIndex: 'createdAtUtc', width: 155, render: (v: string) => formatLocalTime(v) },
    { title: 'Hoàn thành', dataIndex: 'completedAtUtc', width: 155, render: (v: string) => formatLocalTime(v) },
    { title: 'Lỗi', dataIndex: 'lastError', ellipsis: true, render: (v: string) => v || '-' },
    {
      title: 'Thao tác', key: 'actions', width: 120, fixed: 'right' as const,
      render: (_: any, row: JobDto) => (
        <Space size={4}>
          {row.status === 'Failed' && (
            <Popconfirm title="Retry job này?" onConfirm={() => retry(row.id)}>
              <Button size="small" icon={<RedoOutlined />} type="link">Retry</Button>
            </Popconfirm>
          )}
          {['Pending', 'Processing'].includes(row.status) && (
            <Popconfirm title="Huỷ job này?" onConfirm={() => cancel(row.id)}>
              <Button size="small" icon={<StopOutlined />} type="link" danger>Huỷ</Button>
            </Popconfirm>
          )}
        </Space>
      )
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space>
        <Title level={3} style={{ margin: 0 }}>Jobs</Title>
      </Space>
      <Card>
        <Space style={{ marginBottom: 16 }}>
          <Select value={statusFilter} onChange={setFilter} options={STATUS_OPTIONS} style={{ width: 140 }} placeholder="Lọc status" />
          <Button icon={<SyncOutlined />} onClick={() => fetch(page)}>Refresh</Button>
        </Space>
        <Table
          dataSource={jobs}
          columns={columns}
          rowKey="id"
          loading={loading}
          size="small"
          pagination={{ total, current: page, pageSize, onChange: (p) => fetch(p), showTotal: (t) => `${t} jobs` }}
          scroll={{ x: 1100 }}
        />
      </Card>
    </Space>
  );
}
