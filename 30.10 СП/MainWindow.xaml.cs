using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace _30._10_СП
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Node> Nodes { get; set; }
        public ObservableCollection<NetworkPacket> Packets { get; set; }
        private PacketRouter _router;

        private int _processedPacketsCount = 0;
        private int _droppedPacketsCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            Nodes = new ObservableCollection<Node>();
            Packets = new ObservableCollection<NetworkPacket>();
            _router = new PacketRouter(Nodes);

            Nodes.Add(new Node { IpAddress = "192.168.1.1", Capacity = 5, Ellipse = CreateNodeEllipse(50, 100) });
            Nodes.Add(new Node { IpAddress = "192.168.1.2", Capacity = 5, Ellipse = CreateNodeEllipse(250, 100) });

            Task.Run(ProcessPacketsAsync);
            Task.Run(UpdateVisualization);
        }

        private Ellipse CreateNodeEllipse(double x, double y)
        {
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.Blue,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(ellipse, x);
            Canvas.SetTop(ellipse, y);
            NetworkCanvas.Children.Add(ellipse);
            return ellipse;
        }

        private async Task ProcessPacketsAsync()
        {
            Random rand = new Random();
            while (true)
            {
                if (Nodes.Count > 1)
                {
                    string senderIp, receiverIp;
                    do
                    {
                        senderIp = Nodes[rand.Next(Nodes.Count)].IpAddress;
                        receiverIp = Nodes[rand.Next(Nodes.Count)].IpAddress;
                    } while (senderIp == receiverIp);

                    var packet = new NetworkPacket
                    {
                        Id = Packets.Count + 1,
                        Size = rand.Next(50, 150),
                        Type = (PacketType)rand.Next(0, 3),
                        Priority = (PacketPriority)rand.Next(0, 3),
                        SenderIp = senderIp,
                        ReceiverIp = receiverIp
                    };

                    Packets.Add(packet);

                    if (_router.RoutePacket(packet))
                    {
                        _processedPacketsCount++;
                        VisualizePacket(packet);
                    }
                    else
                    {
                        _droppedPacketsCount++;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateStatistics();
                    });
                }

                _router.ProcessAllPackets();
                await Task.Delay(1000);
            }
        }

        private void VisualizePacket(NetworkPacket packet)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var senderNode = Nodes.FirstOrDefault(n => n.IpAddress == packet.SenderIp);
                var receiverNode = Nodes.FirstOrDefault(n => n.IpAddress == packet.ReceiverIp);

                if (senderNode != null && receiverNode != null)
                {
                    var packetEllipse = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = Brushes.Red,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(packetEllipse, Canvas.GetLeft(senderNode.Ellipse));
                    Canvas.SetTop(packetEllipse, Canvas.GetTop(senderNode.Ellipse));
                    NetworkCanvas.Children.Add(packetEllipse);

                    var animation = new DoubleAnimation(Canvas.GetLeft(receiverNode.Ellipse), TimeSpan.FromMilliseconds(1000));
                    animation.Completed += (s, _) => NetworkCanvas.Children.Remove(packetEllipse); // Удаление после завершения
                    packetEllipse.BeginAnimation(Canvas.LeftProperty, animation);
                }
            });
        }

        private void UpdateStatistics()
        {
            StatisticsTextBlock.Text = $"Обработано пакетов: {_processedPacketsCount}";
            DroppedPacketsTextBlock.Text = $"Потеряно пакетов: {_droppedPacketsCount}";
        }

        private void AddNodeButton_Click(object sender, RoutedEventArgs e)
        {
            var newNode = new Node
            {
                IpAddress = $"192.168.1.{Nodes.Count + 1}",
                Capacity = 5,
                Ellipse = CreateNodeEllipse(50 + Nodes.Count * 200, 100)
            };
            Nodes.Add(newNode);
        }

        private void RemoveNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Nodes.Count > 0)
            {
                var nodeToRemove = Nodes.Last();
                NetworkCanvas.Children.Remove(nodeToRemove.Ellipse);
                Nodes.Remove(nodeToRemove);
            }
        }

        private async Task UpdateVisualization()
        {
            while (true)
            {
                await Task.Delay(100);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var node in Nodes)
                    {
                        node.Ellipse.ToolTip = $"Загрузка узла: {node.Load}";
                    }
                });
            }
        }
    }

    public class PacketRouter
    {
        private readonly ObservableCollection<Node> _nodes;

        public PacketRouter(ObservableCollection<Node> nodes)
        {
            _nodes = nodes;
        }

        public bool RoutePacket(NetworkPacket packet)
        {
            var senderNode = _nodes.FirstOrDefault(n => n.IpAddress == packet.SenderIp);
            var receiverNode = _nodes.FirstOrDefault(n => n.IpAddress == packet.ReceiverIp);

            if (senderNode != null && receiverNode != null)
            {
                if (senderNode.AddPacketToBuffer(packet))
                {
                    return true;
                }
            }

            return false;
        }

        public void ProcessAllPackets()
        {
            foreach (var node in _nodes)
            {
                node.ProcessPackets();
            }
        }
    }

    public class Node
    {
        public string IpAddress { get; set; }
        public int Capacity { get; set; }
        public Ellipse Ellipse { get; set; }
        public List<NetworkPacket> PacketBuffer { get; set; }
        public double Load { get; set; }

        public Node()
        {
            PacketBuffer = new List<NetworkPacket>();
        }

        public bool AddPacketToBuffer(NetworkPacket packet)
        {
            if (PacketBuffer.Count < Capacity)
            {
                PacketBuffer.Add(packet);
                Load = (double)PacketBuffer.Count / Capacity;
                return true;
            }

            return false;
        }

        public void ProcessPackets()
        {
            foreach (var packet in PacketBuffer.ToList())
            {
                if (packet.ReceiverIp == IpAddress)
                {
                    PacketBuffer.Remove(packet);
                    Load = (double)PacketBuffer.Count / Capacity;
                }
            }
        }
    }

    public class NetworkPacket
    {
        public int Id { get; set; }
        public int Size { get; set; }
        public PacketType Type { get; set; }
        public PacketPriority Priority { get; set; }
        public string SenderIp { get; set; }
        public string ReceiverIp { get; set; }
    }

    public enum PacketType
    {
        Data,
        Voice,
        Video
    }

    public enum PacketPriority
    {
        Low,
        Medium,
        High
    }
}