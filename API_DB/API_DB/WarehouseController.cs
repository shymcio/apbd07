using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace API_DB.Warehouse;

[ApiController]
[Route("warehouse")]
public class WarehouseController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public WarehouseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        [HttpPost]
        public IActionResult AddProductToWarehouse([FromBody] WarehouseRequest request)
        {
            try
            {
                if (request == null || !ModelState.IsValid)
                {
                    return BadRequest("Invalid request format.");
                }

                using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default")))
                {
                    connection.Open();
                    
                    SqlCommand productCommand = new SqlCommand("SELECT COUNT(*) FROM Product WHERE IdProduct = @IdProduct", connection);
                    productCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    int productCount = (int)productCommand.ExecuteScalar();

                    if (productCount == 0)
                    {
                        return NotFound("Product with the specified IdProduct does not exist.");
                    }
                    
                    SqlCommand warehouseCommand = new SqlCommand("SELECT COUNT(*) FROM Warehouse WHERE IdWarehouse = @IdWarehouse", connection);
                    warehouseCommand.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    int warehouseCount = (int)warehouseCommand.ExecuteScalar();

                    if (warehouseCount == 0)
                    {
                        return NotFound("Warehouse with the specified IdWarehouse does not exist.");
                    }
                    
                    if (request.Amount <= 0)
                    {
                        return BadRequest("Amount must be greater than 0.");
                    }
                    
                    SqlCommand orderCommand = new SqlCommand("SELECT COUNT(*) FROM [Order] WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt <= @CreatedAt", connection);
                    orderCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    orderCommand.Parameters.AddWithValue("@Amount", request.Amount);
                    orderCommand.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
                    int orderCount = (int)orderCommand.ExecuteScalar();

                    if (orderCount == 0)
                    {
                        return BadRequest("Order for the specified product does not exist or was created after the request.");
                    }
                    
                    SqlCommand fulfilledCommand = new SqlCommand("SELECT COUNT(*) FROM [Order] WHERE IdProduct = @IdProduct AND FulfilledAt IS NOT NULL", connection);
                    fulfilledCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    int fulfilledCount = (int)fulfilledCommand.ExecuteScalar();

                    if (fulfilledCount == 0)
                    {
                        return BadRequest("Order for the specified product has not been fulfilled.");
                    }
                    
                    SqlCommand updateCommand = new SqlCommand("UPDATE [Order] SET FullfilledAt = GETDATE() WHERE IdProduct = @IdProduct", connection);
                    updateCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    updateCommand.ExecuteNonQuery();
                    
                    SqlCommand insertCommand = new SqlCommand("INSERT INTO Product_Warehouse (IdProduct, IdWarehouse, Amount, Price, CreatedAt) VALUES (@IdProduct, @IdWarehouse, @Amount, @Price, GETDATE()); SELECT SCOPE_IDENTITY();", connection);
                    insertCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    insertCommand.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    insertCommand.Parameters.AddWithValue("@Amount", request.Amount);
                    insertCommand.Parameters.AddWithValue("@Price", GetProductPrice(request.IdProduct) * request.Amount);
                    
                    int insertedId = Convert.ToInt32(insertCommand.ExecuteScalar());

                    return Ok($"Product successfully added to warehouse. Product_Warehouse Id: {insertedId}");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        
        private decimal GetProductPrice(int productId)
        {
            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default")))
            {
                connection.Open();
        
                SqlCommand command = new SqlCommand("SELECT Price FROM Product WHERE IdProduct = @IdProduct", connection);
                command.Parameters.AddWithValue("@IdProduct", productId);
        
                object result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToDecimal(result);
                }
                else
                {
                    throw new Exception("Price for the specified product could not be found.");
                }
            }
        }
    }