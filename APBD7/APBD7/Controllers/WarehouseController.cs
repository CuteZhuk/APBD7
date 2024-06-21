using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using APBD7.DB;
using APBD7.Models;
using Microsoft.EntityFrameworkCore;

namespace APBD7.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WarehouseController : ControllerBase
    {
        private readonly WarehouseContext _context;
        private readonly IConfiguration _configuration;

        public WarehouseController(WarehouseContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("AddProductToWarehouse")]
        public async Task<IActionResult> AddProductToWarehouse([FromBody] AddProductToWarehouseRequest request)
        {
            // Validate request
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than 0.");

            // Check if the product exists
            var product = await _context.Products.FindAsync(request.IdProduct);
            if (product == null)
                return NotFound("Product not found.");

            // Check if the warehouse exists
            var warehouse = await _context.Warehouses.FindAsync(request.IdWarehouse);
            if (warehouse == null)
                return NotFound("Warehouse not found.");

            // Check if there is a matching order
            var order = await _context.Orders
                .FirstOrDefaultAsync(o =>
                    o.IdProduct == request.IdProduct && o.Amount == request.Amount && o.CreatedAt < request.CreatedAt &&
                    o.FulfilledAt == null);
            if (order == null)
                return NotFound("No matching order found.");

            // Check if the order is already fulfilled
            var existingProductWarehouse = await _context.ProductWarehouses
                .FirstOrDefaultAsync(pw => pw.IdOrder == order.IdOrder);
            if (existingProductWarehouse != null)
                return BadRequest("Order is already fulfilled.");

            // Update order as fulfilled
            order.FulfilledAt = DateTime.Now;
            _context.Orders.Update(order);

            // Add product to warehouse
            var productWarehouse = new ProductWarehouse
            {
                IdWarehouse = request.IdWarehouse,
                IdProduct = request.IdProduct,
                IdOrder = order.IdOrder,
                Amount = request.Amount,
                Price = product.Price * request.Amount,
                CreatedAt = DateTime.Now
            };
            _context.ProductWarehouses.Add(productWarehouse);

            await _context.SaveChangesAsync();

            return Ok(new { NewId = productWarehouse.IdProductWarehouse });
        }

        [HttpPost]
        [Route("AddProductToWarehouseStoredProcedure")]
        public async Task<IActionResult> AddProductToWarehouseStoredProcedure(
            [FromBody] AddProductToWarehouseRequest request)
        {
            // Validate request
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than 0.");
            
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("AddProductToWarehouse", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    command.Parameters.AddWithValue("@Amount", request.Amount);
                    command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

                    try
                    {
                        var newId = (int)await command.ExecuteScalarAsync();
                        return Ok(new { NewId = newId });
                    }
                    catch (SqlException ex)
                    {
                        return StatusCode(500, ex.Message);
                    }
                }
            }
        }
    }
}


