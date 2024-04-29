using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Threading.Tasks;

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
        public async Task<IActionResult> AddProductToWarehouse([FromBody] WarehouseRequest request)
{
    try
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest("Invalid request format.");
        }

        using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default")))
        {
            await connection.OpenAsync();

            // Step 1: Check if the order has been fulfilled
            SqlCommand fulfilledCommand = new SqlCommand("SELECT COUNT(*) FROM [Order] WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt < @CreatedAt AND FulfilledAt IS NOT NULL", connection);
            fulfilledCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            fulfilledCommand.Parameters.AddWithValue("@Amount", request.Amount);
            fulfilledCommand.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
            int fulfilledCount = (int)await fulfilledCommand.ExecuteScalarAsync();

            if (fulfilledCount == 0)
            {
                return BadRequest("Order for the specified product does not exist or was created after the request.");
            }

            // Step 2: Check if the order already exists in Product_Warehouse
            SqlCommand existingOrderCommand = new SqlCommand("SELECT COUNT(*) FROM Product_Warehouse WHERE IdProduct = @IdProduct", connection);
            existingOrderCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            int existingOrderCount = (int)await existingOrderCommand.ExecuteScalarAsync();

            if (existingOrderCount > 0)
            {
                return BadRequest("Order already exists in Product_Warehouse.");
            }

            // Step 3: Insert a new record into Product_Warehouse
            SqlCommand insertCommand = new SqlCommand("INSERT INTO Product_Warehouse (IdProduct, IdWarehouse, Amount, Price, CreatedAt) VALUES (@IdProduct, @IdWarehouse, @Amount, @Price, GETDATE()); SELECT SCOPE_IDENTITY();", connection);
            insertCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            insertCommand.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            insertCommand.Parameters.AddWithValue("@Amount", request.Amount);
            decimal productPrice = await GetProductPriceAsync(request.IdProduct);
            insertCommand.Parameters.AddWithValue("@Price", productPrice * request.Amount);
            int insertedId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());

            return Ok($"Product successfully added to warehouse. Product_Warehouse Id: {insertedId}");
        }
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"Internal server error: {ex.Message}");
    }
}

        
        private async Task<decimal> GetProductPriceAsync(int productId)
        {
            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default")))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand("SELECT Price FROM Product WHERE IdProduct = @IdProduct", connection);
                command.Parameters.AddWithValue("@IdProduct", productId);

                object result = await command.ExecuteScalarAsync();
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