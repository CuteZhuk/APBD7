using Microsoft.AspNetCore.Mvc;
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace APBD7.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WarehouseController : ControllerBase
    {
        private readonly string _connectionString = "Your_Connection_String_Here";

        [HttpPost]
        public async Task<IActionResult> AddProductToWarehouse([FromBody] ProductWarehouseRequest request)
        {
            if (request == null || request.IdProduct <= 0 || request.IdWarehouse <= 0 || request.Amount <= 0)
            {
                return BadRequest("Invalid request data");
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Check if product exists
                        var productExists = await CheckIfProductExists(request.IdProduct, connection, transaction);
                        if (!productExists)
                        {
                            return NotFound("Product not found");
                        }

                        // Step 2: Check if warehouse exists
                        var warehouseExists = await CheckIfWarehouseExists(request.IdWarehouse, connection, transaction);
                        if (!warehouseExists)
                        {
                            return NotFound("Warehouse not found");
                        }

                        // Step 3: Check if there is a matching order
                        var orderId = await CheckIfOrderExists(request.IdProduct, request.Amount, request.CreatedAt, connection, transaction);
                        if (orderId == null)
                        {
                            return BadRequest("No matching order found");
                        }

                        // Step 4: Check if order is already fulfilled
                        var orderFulfilled = await CheckIfOrderFulfilled(orderId.Value, connection, transaction);
                        if (orderFulfilled)
                        {
                            return BadRequest("Order is already fulfilled");
                        }

                        // Step 5: Update FulfilledAt in Order table
                        await UpdateOrderFulfilledAt(orderId.Value, connection, transaction);

                        // Step 6: Insert into Product_Warehouse table and return the new record ID
                        var newProductWarehouseId = await InsertProductWarehouseRecord(request, orderId.Value, connection, transaction);

                        await transaction.CommitAsync();

                        return Ok(new { ProductWarehouseId = newProductWarehouseId });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }

        private async Task<bool> CheckIfProductExists(int productId, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand("SELECT COUNT(1) FROM Product WHERE IdProduct = @IdProduct", connection, transaction);
            command.Parameters.AddWithValue("@IdProduct", productId);
            return (int)await command.ExecuteScalarAsync() > 0;
        }

        private async Task<bool> CheckIfWarehouseExists(int warehouseId, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand("SELECT COUNT(1) FROM Warehouse WHERE IdWarehouse = @IdWarehouse", connection, transaction);
            command.Parameters.AddWithValue("@IdWarehouse", warehouseId);
            return (int)await command.ExecuteScalarAsync() > 0;
        }

        private async Task<int?> CheckIfOrderExists(int productId, int amount, DateTime createdAt, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand("SELECT TOP 1 IdOrder FROM [Order] WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt < @CreatedAt", connection, transaction);
            command.Parameters.AddWithValue("@IdProduct", productId);
            command.Parameters.AddWithValue("@Amount", amount);
            command.Parameters.AddWithValue("@CreatedAt", createdAt);
            var result = await command.ExecuteScalarAsync();
            return result != DBNull.Value ? (int?)result : null;
        }

        private async Task<bool> CheckIfOrderFulfilled(int orderId, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand("SELECT COUNT(1) FROM Product_Warehouse WHERE IdOrder = @IdOrder", connection, transaction);
            command.Parameters.AddWithValue("@IdOrder", orderId);
            return (int)await command.ExecuteScalarAsync() > 0;
        }

        private async Task UpdateOrderFulfilledAt(int orderId, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand("UPDATE [Order] SET FulfilledAt = @FulfilledAt WHERE IdOrder = @IdOrder", connection, transaction);
            command.Parameters.AddWithValue("@FulfilledAt", DateTime.Now);
            command.Parameters.AddWithValue("@IdOrder", orderId);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<int> InsertProductWarehouseRecord(ProductWarehouseRequest request, int orderId, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand(
                "INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt) " +
                "VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, (SELECT Price FROM Product WHERE IdProduct = @IdProduct) * @Amount, @CreatedAt);" +
                "SELECT CAST(scope_identity() AS int);", connection, transaction);
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            command.Parameters.AddWithValue("@IdOrder", orderId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            return (int)await command.ExecuteScalarAsync();
        }

        // Endpoint for executing the stored procedure
        [HttpPost("AddProductToWarehouseWithProc")]
        public async Task<IActionResult> AddProductToWarehouseWithProc([FromBody] ProductWarehouseRequest request)
        {
            if (request == null || request.IdProduct <= 0 || request.IdWarehouse <= 0 || request.Amount <= 0)
            {
                return BadRequest("Invalid request data");
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("AddProductToWarehouse", connection))
                {
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    command.Parameters.AddWithValue("@Amount", request.Amount);
                    command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

                    try
                    {
                        var result = await command.ExecuteScalarAsync();
                        return Ok(new { NewId = result });
                    }
                    catch (SqlException ex)
                    {
                        return StatusCode(500, $"Stored procedure execution error: {ex.Message}");
                    }
                }
            }
        }
    }
}


