using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsApp
{
    public partial class PCPartsUser : UserControl
    {
        private string customer_id;
        private string connectionString = "Data Source=DESKTOP-ORBKK1T\\SQLEXPRESS; Initial Catalog=SmartKroy; Integrated Security=True; Trust Server Certificate=True";

        public PCPartsUser(string customer_id)
        {
            InitializeComponent();
            InitializeDataGridView();
            LoadElectronicProducts();
            this.customer_id = customer_id;
        }

        private void InitializeDataGridView()
        {
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.Columns.Clear();

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                Name = "product_id",
                DataPropertyName = "product_id",
                HeaderText = "Product ID",
                Width = 100
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                Name = "product_name",
                DataPropertyName = "product_name",
                HeaderText = "Product Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                Name = "product_price",
                DataPropertyName = "product_price",
                HeaderText = "Price",
                Width = 80
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                Name = "product_quantity",
                DataPropertyName = "product_quantity",
                HeaderText = "Available",
                Width = 80
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn()
            {
                Name = "sup_id",
                DataPropertyName = "sup_id",
                HeaderText = "Supplier ID",
                Width = 100
            });

            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
        }

        public void LoadElectronicProducts()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT p.product_id, p.product_name, p.product_price, 
                                   p.product_quantity, p.sup_id
                                   FROM products p
                                   JOIN product_catagory c ON p.catagory_id = c.catagory_id
                                   WHERE c.catagory_name = 'Electronic' 
                                   AND p.product_name LIKE '%(approved)%'";

                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    dataGridView1.DataSource = table;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading clothing products: " + ex.Message);
            }
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
                guna2TextBox1.Text = row.Cells["product_id"].Value?.ToString();
                guna2TextBox2.Text = row.Cells["product_name"].Value?.ToString();
                guna2TextBox3.Text = row.Cells["product_price"].Value?.ToString();
                guna2TextBox4.Text = "1"; 
            }
        }

        private void btn_add_to_cart_Click(object sender, EventArgs e)
        {

            if (string.IsNullOrEmpty(guna2TextBox1.Text))
            {
                MessageBox.Show("Please select a product first", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!int.TryParse(guna2TextBox4.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity (1 or more)", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string productId = guna2TextBox1.Text;
            string productName = guna2TextBox2.Text;
            string productPrice = guna2TextBox3.Text;
            string productQuantity = guna2TextBox4.Text;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlTransaction transaction = connection.BeginTransaction();

                    try
                    {
                      
                        string getCartQuery = "SELECT cart_id FROM registration WHERE customer_id = @customerId";
                        int cartId = 0;
                        using (SqlCommand cartCmd = new SqlCommand(getCartQuery, connection, transaction))
                        {
                            cartCmd.Parameters.AddWithValue("@customerId", customer_id);
                            object result = cartCmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                cartId = Convert.ToInt32(result);
                            }
                        }

                        if (cartId == 0)
                        {
                        
                            string createCartQuery = @"INSERT INTO Cart DEFAULT VALUES; SELECT SCOPE_IDENTITY();";
                            using (SqlCommand createCmd = new SqlCommand(createCartQuery, connection, transaction))
                            {
                                cartId = Convert.ToInt32(createCmd.ExecuteScalar());
                            }

                    
                            string assignCartQuery = "UPDATE registration SET cart_id = @cartId WHERE customer_id = @customerId";
                            using (SqlCommand assignCmd = new SqlCommand(assignCartQuery, connection, transaction))
                            {
                                assignCmd.Parameters.AddWithValue("@cartId", cartId);
                                assignCmd.Parameters.AddWithValue("@customerId", customer_id);
                                assignCmd.ExecuteNonQuery();
                            }
                        }

                        string checkQuantityQuery = "SELECT product_quantity FROM products WHERE product_id = @productId";
                        using (SqlCommand checkCmd = new SqlCommand(checkQuantityQuery, connection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@productId", productId);
                            int availableQuantity = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (availableQuantity < quantity)
                            {
                                MessageBox.Show($"Only {availableQuantity} items available in stock", "Stock Error",
                                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                transaction.Rollback();
                                return;
                            }
                        }

                        string insertQuery = @"INSERT INTO added (product_id, product_name, product_price, 
                                    product_quantity, sup_id)
                                    VALUES (@productId, @productName, @productPrice, 
                                    @quantity, 
                                    (SELECT sup_id FROM products WHERE product_id = @productId))";
                        using (SqlCommand insertCmd = new SqlCommand(insertQuery, connection, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@productId", productId);
                            insertCmd.Parameters.AddWithValue("@productName", productName);
                            insertCmd.Parameters.AddWithValue("@productPrice", productPrice);
                            insertCmd.Parameters.AddWithValue("@quantity", quantity);
                            insertCmd.ExecuteNonQuery();
                        }

                        string updateProductQuery = @"UPDATE products 
                                           SET product_quantity = product_quantity - @quantity,
                                               cart_id = @cartId
                                           WHERE product_id = @productId";
                        using (SqlCommand updateCmd = new SqlCommand(updateProductQuery, connection, transaction))
                        {
                            updateCmd.Parameters.AddWithValue("@productId", productId);
                            updateCmd.Parameters.AddWithValue("@quantity", quantity);
                            updateCmd.Parameters.AddWithValue("@cartId", cartId);
                            int rowsAffected = updateCmd.ExecuteNonQuery();

                            if (rowsAffected == 0)
                            {
                                MessageBox.Show("Product not found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                transaction.Rollback();
                                return;
                            }
                        }

                        transaction.Commit();
                        MessageBox.Show($"{quantity} {productName}(s) added to cart successfully!",
                                      "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        LoadElectronicProducts();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Error adding to cart: {ex.Message}", "Error",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database connection error: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox2_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox3_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox4_TextChanged(object sender, EventArgs e) { }
    }
}