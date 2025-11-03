using System.Windows;

namespace SecureChatClientGUI
{
    public partial class CreateGroupWindow : Window
    {
        public CreateGroupWindow()
        {
            InitializeComponent();
        }

        public string GroupName => GroupNameTextBox.Text;
        public string MembersCsv => MembersTextBox.Text;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupName))
            {
                MessageBox.Show("Vui lòng nhập tên nhóm.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
