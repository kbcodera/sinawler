using System;
using System.Collections.Generic;
using System.Text;
using Sina.Api;
using System.Threading;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using Sinawler.Model;
using System.Data;

namespace Sinawler
{
    class StatusRobot
    {
        private SinaApiService api;
        private bool blnAsyncCancelled = false;     //ָʾ�����߳��Ƿ�ȡ������������ֹ����ѭ��
        private string strLogFile = Application.StartupPath + "\\" + DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() + DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString() + "_status.log";             //��־�ļ�
        private string strLog = "";                 //��־����

        private LinkedList<long> lstWaitingUID = new LinkedList<long>();     //�ȴ����е�UID����
        private int iQueueLength = 5000;               //�ڴ��ж��г������ޣ�Ĭ��5000

        private int iPreLoadQueue = (int)(EnumPreLoadQueue.NO_PRELOAD);       //�Ƿ�����ݿ���Ԥ�����û����С�Ĭ��Ϊ����
        private bool blnSuspending = false;         //�Ƿ���ͣ��Ĭ��Ϊ����

        private SinaMBCrawler crawler;              //������󡣹��캯���г�ʼ��

        private int iInitQueueLength = 100;          //��ʼ���г���

        private QueueBuffer queueBuffer = new QueueBuffer( QueueBufferTarget.FOR_STATUS );    //���ݿ���л���
        private long lCommentUID = 0;               //��ȡ�������е�UID����ʱ�׳���UserRobot
        BackgroundWorker bwAsync = null;

        //���캯������Ҫ������Ӧ������΢��API��������
        public StatusRobot ( SinaApiService oAPI )
        {
            this.api = oAPI;

            SettingItems settings = AppSettings.Load();
            if (settings == null) settings = AppSettings.LoadDefault();
            iQueueLength = settings.QueueLength;

            crawler = new SinaMBCrawler( this.api );
        }

        public bool AsyncCancelled
        {
            set { blnAsyncCancelled = value; }
            get { return blnAsyncCancelled; }
        }

        public string LogFile
        {
            set { strLogFile = value; }
            get { return strLogFile; }
        }

        public int QueueLength
        { set { iQueueLength = value; } }

        public int InitQueueLength
        { get { return iInitQueueLength; } }

        public EnumPreLoadQueue PreLoadQueue
        {
            get { return (EnumPreLoadQueue)iPreLoadQueue; }
            set { iPreLoadQueue = (int)value; }
        }

        public bool Suspending
        {
            get { return blnSuspending; }
            set { blnSuspending = value; }
        }

        //��������API�Ľӿ�
        public SinaApiService SinaAPI
        { set { api = value; } }

        public long ThrownUID
        { get { return lCommentUID; } }

        public BackgroundWorker AsyncWorker
        { set { bwAsync = value; } }

        //д��־�ļ���Ҳ���������ı�������ʾ��־
        //oControl������ΪͬʱҪ�����Ŀؼ�
        public void Actioned ( Object oControl )
        {
            //д����־�ļ�
            StreamWriter sw = File.AppendText( strLogFile );
            sw.WriteLine( strLog );
            sw.Close();
            sw.Dispose();

            //���ı�����׷��
            Label lblLog = (Label)oControl;
            lblLog.Text = strLog;
        }

        /// <summary>
        /// ���ⲿ��ȡUID�ӵ��Լ�������
        /// </summary>
        /// <param name="lUid"></param>
        public void Enqueue ( long lUID )
        {
            if (lstWaitingUID.Contains( lUID ) || queueBuffer.Contains( lUID ))
            {
                //��־
                strLog = DateTime.Now.ToString() + "  " + "�û�" + lUID.ToString() + "���ڶ�����...";
                bwAsync.ReportProgress( 100 );
            }
            else
            {
                //��־
                strLog = DateTime.Now.ToString() + "  " + "���û�" + lUID + "������С��ڴ��������" + lstWaitingUID.Count + "���û������ݿ��������" + queueBuffer.Count.ToString() + "���û�";
                bwAsync.ReportProgress( 100 );
                //���ڴ����Ѵﵽ���ޣ���ʹ�����ݿ���л���
                //����ʹ�����ݿ���л���
                if (lstWaitingUID.Count < iQueueLength)
                    lstWaitingUID.AddLast( lUID );
                else
                    queueBuffer.Enqueue( lUID );
            }
            Thread.Sleep( 5 );
        }

        /// <summary>
        /// ��ָ����UIDΪ��㿪ʼ����
        /// </summary>
        /// <param name="lUid"></param>
        public void Start ( long lStartUID )
        {
            if (lStartUID == 0) return;

            //����ѡ�ѡ������û����еķ���
            DataTable dtUID=new DataTable();

            switch (iPreLoadQueue)
            {
                case (int)EnumPreLoadQueue.PRELOAD_UID:
                    //��־
                    strLog = DateTime.Now.ToString() + "  " + "��ʼ���û����У���ȡ����ȡ���ݵ��û���ID���������ڴ����...";
                    bwAsync.ReportProgress( 0 );
                    Thread.Sleep( 5 );
                    dtUID = User.GetCrawedUIDTable();
                    break;
                case (int)EnumPreLoadQueue.PRELOAD_ALL_UID:
                    //��־
                    strLog = DateTime.Now.ToString() + "  " + "��ʼ���û����У���ȡ���ݿ��������û���ID���������ڴ����...";
                    bwAsync.ReportProgress( 0 );
                    Thread.Sleep( 5 );
                    dtUID = UserRelation.GetAllUIDTable();
                    break;
            }

            if (dtUID != null)
            {
                iInitQueueLength = dtUID.Rows.Count;
                long lUID;
                int i;
                for (i = 0; i < dtUID.Rows.Count && lstWaitingUID.Count < iQueueLength; i++)
                {
                    if (blnAsyncCancelled) return;
                    while (blnSuspending) Thread.Sleep( 50 );
                    lUID = Convert.ToInt64( dtUID.Rows[i]["uid"] );
                    if (!lstWaitingUID.Contains( lUID ))
                    {
                        //��־
                        strLog = DateTime.Now.ToString() + "  " + "��ʼ���û����У����û�" + lUID.ToString() + "������С��ڴ��������" + lstWaitingUID.Count + "���û������ݿ��������" + queueBuffer.Count.ToString() + "���û������ȣ�" + ((int)((float)((i + 1) * 100) / (float)iInitQueueLength)).ToString() + "%";
                        bwAsync.ReportProgress( 5 );
                        Thread.Sleep( 5 );
                        lstWaitingUID.AddLast( lUID );
                    }
                }

                //������ʣ�࣬�����ಿ�ַ������ݿ���л���
                while (i < dtUID.Rows.Count)
                {
                    if (blnAsyncCancelled) return;
                    while (blnSuspending) Thread.Sleep( 50 );
                    lUID = Convert.ToInt64( dtUID.Rows[i]["uid"] );
                    int iLengthInDB = queueBuffer.Count;
                    if (!queueBuffer.Contains( lUID ))
                    {
                        queueBuffer.Enqueue( lUID );
                        ++iLengthInDB;
                        //��־
                        strLog = DateTime.Now.ToString() + "  " + "��ʼ���û����У��ڴ�������������û�" + lUID.ToString() + "�������ݿ���У����ݿ��������" + iLengthInDB.ToString() + "���û������ȣ�" + ((int)((float)((i + 1) * 100) / (float)iInitQueueLength)).ToString() + "%";
                        bwAsync.ReportProgress( 5 );
                        Thread.Sleep( 5 );
                    }
                    i++;
                }
            }
            dtUID.Dispose();

            //�Ӷ�����ȥ����ǰUID
            lstWaitingUID.Remove( lStartUID );
            //����ǰUID�ӵ���ͷ
            lstWaitingUID.AddFirst( lStartUID );
            //��־
            strLog = DateTime.Now.ToString() + "  " + "��ʼ���û�������ɡ�";
            bwAsync.ReportProgress( 100 );
            Thread.Sleep( 5 );
            long lCurrentUID = lStartUID;
            //�Զ���ѭ������
            while (lstWaitingUID.Count > 0)
            {
                if (blnAsyncCancelled) return;
                while (blnSuspending) Thread.Sleep( 50 );
                //����ͷȡ��
                lCurrentUID = lstWaitingUID.First.Value;
                lstWaitingUID.RemoveFirst();
                //�����ݿ���л���������Ԫ��
                long lHead = queueBuffer.Dequeue();
                if (lHead > 0)
                    lstWaitingUID.AddLast( lHead );
                #region Ԥ����
                if (lCurrentUID == lStartUID)  //˵������һ��ѭ������
                {
                    if (blnAsyncCancelled) return;
                    while (blnSuspending) Thread.Sleep( 50 );

                    //��־
                    strLog = DateTime.Now.ToString() + "  " + "��ʼ����֮ǰ���ӵ�������...";
                    bwAsync.ReportProgress( 100 );
                    Thread.Sleep( 5 );

                    Status.NewIterate();
                    Comment.NewIterate();
                }

                #endregion                
                #region �û�΢����Ϣ
                if (blnAsyncCancelled) return;
                while (blnSuspending) Thread.Sleep( 50 );
                //��־
                strLog = DateTime.Now.ToString() + "  " + "��ȡ���ݿ����û�" + lCurrentUID.ToString() + "����һ��΢����ID...";
                bwAsync.ReportProgress( 100 );
                Thread.Sleep( 5 );
                //��ȡ���ݿ��е�ǰ�û�����һ��΢����ID
                long lLastStatusIDOf = Status.GetLastStatusIDOf( lCurrentUID );

                ///@2010-10-11
                ///���ǵ���ֹ����ʱ���ܻ��ж����۵ı��棬�ʴ˴���������ȡ����һ��΢��������
                #region ��ȡ���ݿ�������һ��΢��������
                if (lLastStatusIDOf > 0)
                {
                    if (blnAsyncCancelled) return;
                    while (blnSuspending) Thread.Sleep( 50 );

                    //��־
                    strLog = DateTime.Now.ToString() + "  " + "��ȡ΢��" + lLastStatusIDOf.ToString() + "������...";
                    bwAsync.ReportProgress( 100 );
                    Thread.Sleep( 5 );
                    //��ȡ��ǰ΢��������
                    List<Comment> lstComment = crawler.GetCommentsOf( lLastStatusIDOf );
                    //��־
                    strLog = DateTime.Now.ToString() + "  " + "����" + lstComment.Count.ToString() + "�����ۡ�";
                    bwAsync.ReportProgress( 100 );
                    Thread.Sleep( 5 );
                    if (blnAsyncCancelled) return;
                    while (blnSuspending) Thread.Sleep( 50 );

                    foreach (Comment comment in lstComment)
                    {
                        //Thread.Sleep( 5 );
                        if (blnAsyncCancelled) return;
                        while (blnSuspending) Thread.Sleep( 50 );
                        if (!Comment.Exists( comment.comment_id ))
                        {
                            //��־
                            strLog = DateTime.Now.ToString() + "  " + "������" + comment.comment_id.ToString() + "�������ݿ�...";
                            bwAsync.ReportProgress( 100 );
                            Thread.Sleep( 5 );
                            comment.Add();

                            //�������˼�����С���2010-10-11
                            //��־
                            strLog = DateTime.Now.ToString() + "  " + "��������" + comment.uid.ToString() + "�������...";
                            bwAsync.ReportProgress( 100 );
                            Thread.Sleep( 5 );
                            lCommentUID = comment.uid;
                            if (lstWaitingUID.Contains( lCommentUID ) || queueBuffer.Contains( lCommentUID ))
                            {
                                //��־
                                strLog = DateTime.Now.ToString() + "  " + "�û�" + lCommentUID.ToString() + "���ڶ�����...";
                                bwAsync.ReportProgress( 100 );
                            }
                            else
                            {
                                //��־
                                strLog = DateTime.Now.ToString() + "  " + "���û�" + lCommentUID.ToString() + "������С��ڴ��������" + lstWaitingUID.Count + "���û������ݿ��������" + queueBuffer.Count.ToString() + "���û�";
                                bwAsync.ReportProgress( 100 );
                                //���ڴ����Ѵﵽ���ޣ���ʹ�����ݿ���л���
                                //����ʹ�����ݿ���л���
                                if (lstWaitingUID.Count < iQueueLength)
                                    lstWaitingUID.AddLast( lCommentUID );
                                else
                                    queueBuffer.Enqueue( lCommentUID );
                            }
                        }
                    }
                }
                #endregion

                if (blnAsyncCancelled) return;
                while (blnSuspending) Thread.Sleep( 50 );
                //��־
                strLog = DateTime.Now.ToString() + "  " + "��ȡ�û�" + lCurrentUID.ToString() + "��ID��" + lLastStatusIDOf.ToString() + "֮���΢��...";
                bwAsync.ReportProgress( 100 );
                Thread.Sleep( 5 );
                //��ȡ���ݿ��е�ǰ�û�����һ��΢����ID֮���΢�����������ݿ�
                List<Status> lstStatus = crawler.GetStatusesOfSince( lCurrentUID, lLastStatusIDOf );
                //��־
                strLog = DateTime.Now.ToString() + "  " + "����" + lstStatus.Count.ToString() + "��΢����";
                bwAsync.ReportProgress( 100 );
                Thread.Sleep( 5 );
                #endregion
                #region ΢����Ӧ����
                foreach (Status status in lstStatus)
                {
                    //Thread.Sleep( 5 );
                    if (blnAsyncCancelled) return;
                    while (blnSuspending) Thread.Sleep( 50 );
                    if (!Status.Exists( status.status_id ))
                    {
                        //��־
                        strLog = DateTime.Now.ToString() + "  " + "��΢��" + status.status_id.ToString() + "�������ݿ�...";
                        bwAsync.ReportProgress( 100 );
                        Thread.Sleep( 5 );
                        status.Add();
                    }
                    if (blnAsyncCancelled) return;
                    while (blnSuspending) Thread.Sleep( 50 );

                    //��־
                    strLog = DateTime.Now.ToString() + "  " + "��ȡ΢��" + status.status_id.ToString() + "������...";
                    bwAsync.ReportProgress( 100 );
                    Thread.Sleep( 5 );
                    //��ȡ��ǰ΢��������
                    List<Comment> lstComment = crawler.GetCommentsOf( status.status_id );
                    //��־
                    strLog = DateTime.Now.ToString() + "  " + "����" + lstComment.Count.ToString() + "�����ۡ�";
                    bwAsync.ReportProgress( 100 );
                    Thread.Sleep( 5 );

                    foreach (Comment comment in lstComment)
                    {
                        //Thread.Sleep( 5 );
                        if (blnAsyncCancelled) return;
                        while (blnSuspending) Thread.Sleep( 50 );
                        if (!Comment.Exists( comment.comment_id ))
                        {
                            //��־
                            strLog = DateTime.Now.ToString() + "  " + "������" + comment.comment_id.ToString() + "�������ݿ�...";
                            bwAsync.ReportProgress( 100 );
                            Thread.Sleep( 5 );
                            comment.Add();

                            //�������˼�����С���2010-10-11
                            //��־
                            strLog = DateTime.Now.ToString() + "  " + "��������" + comment.uid.ToString() + "�������...";
                            bwAsync.ReportProgress( 100 );
                            Thread.Sleep( 5 );
                            long lCommentUID = comment.uid;
                            if (lstWaitingUID.Contains( lCommentUID ) || queueBuffer.Contains( lCommentUID ))
                            {
                                //��־
                                strLog = DateTime.Now.ToString() + "  " + "�û�" + lCommentUID.ToString() + "���ڶ�����...";
                                bwAsync.ReportProgress( 100 );
                            }
                            else
                            {
                                //��־
                                strLog = DateTime.Now.ToString() + "  " + "���û�" + lCommentUID.ToString() + "������С��ڴ��������" + lstWaitingUID.Count + "���û������ݿ��������" + queueBuffer.Count.ToString() + "���û�";
                                bwAsync.ReportProgress( 100 );
                                //���ڴ����Ѵﵽ���ޣ���ʹ�����ݿ���л���
                                //����ʹ�����ݿ���л���
                                if (lstWaitingUID.Count < iQueueLength)
                                    lstWaitingUID.AddLast( lCommentUID );
                                else
                                    queueBuffer.Enqueue( lCommentUID );
                            }
                            Thread.Sleep( 5 );
                        }
                    }
                }
                #endregion                
                //����ٽ��ո��������UID�����β
                //��־
                strLog = DateTime.Now.ToString() + "  " + "�û�" + lCurrentUID.ToString() + "����������ȡ��ϣ���������β...";
                bwAsync.ReportProgress( 100 );
                Thread.Sleep( 5 );
                //���ڴ����Ѵﵽ���ޣ���ʹ�����ݿ���л���
                if (lstWaitingUID.Count < iQueueLength)
                    lstWaitingUID.AddLast( lCurrentUID );
                else
                    queueBuffer.Enqueue( lCurrentUID );
                //��������Ƶ��
                //����û�����Ƶ��
                crawler.AdjustFreq();
                //��־
                strLog = DateTime.Now.ToString() + "  " + "����������Ϊ" + crawler.SleepTime.ToString() + "���롣��Сʱʣ��" + crawler.ResetTimeInSeconds.ToString() + "�룬ʣ���������Ϊ" + crawler.RemainingHits.ToString() + "��";
                bwAsync.ReportProgress( 100 );
                Thread.Sleep( 5 );
            }
        }

        public void Initialize ()
        {
            //��ʼ����Ӧ����
            blnAsyncCancelled = false;
            if (lstWaitingUID != null) lstWaitingUID.Clear();

            //������ݿ���л���
            queueBuffer.Clear();
        }
    }
}