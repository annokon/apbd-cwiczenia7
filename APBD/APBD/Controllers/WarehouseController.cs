using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using APBD.Models.DTOs;

namespace APBD.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public WarehouseController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost]
    public IActionResult AddProductToWarehouse(ProductWarehouseDTO request)
    {
        // Otwieramy połączenie
        using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        connection.Open();

        // Definiujemy commanda
        using SqlCommand command = new SqlCommand();
        command.Connection = connection;

        // Sprawdzamy, czy produkt o podanym identyfikatorze istnieje
        command.CommandText = "SELECT COUNT(*) FROM Product WHERE IdProduct = @IdProduct";
        command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
        int productCount = (int)command.ExecuteScalar();
        if (productCount == 0)
        {
            return NotFound("Produkt o podanym identyfikatorze nie istnieje.");
        }

        // Sprawdzamy, czy magazyn o podanym identyfikatorze istnieje
        command.CommandText = "SELECT COUNT(*) FROM Warehouse WHERE IdWarehouse = @IdWarehouse";
        command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
        int warehouseCount = (int)command.ExecuteScalar();
        if (warehouseCount == 0)
        {
            return NotFound("Magazyn o podanym identyfikatorze nie istnieje.");
        }

        // Sprawdzamy, czy istnieje zamówienie zakupu produktu w tabeli Order
        command.CommandText =
            "SELECT IdOrder, Amount FROM [Order] WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt < @CreatedAt";
        command.Parameters.AddWithValue("@Amount", request.Amount);
        command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
        SqlDataReader reader = command.ExecuteReader();
        if (!reader.HasRows)
        {
            reader.Close();
            return NotFound("Nie istnieje zamówienie zakupu produktu.");
        }

        reader.Read();
        int orderId = reader.GetInt32(0);
        reader.Close();

        // Aktualizujemy kolumnę FullfilledAt zamówienia na aktualną datę i godzinę
        command.CommandText = "UPDATE [Order] SET FulfilledAt = @FulfilledAt WHERE IdOrder = @IdOrder";
        command.Parameters.AddWithValue("@FulfilledAt", DateTime.Now);
        command.Parameters.AddWithValue("@IdOrder", orderId);
        command.ExecuteNonQuery();

        // Obliczamy cenę na podstawie ilości i ceny z tabeli Product
        command.CommandText = "SELECT Price FROM Product WHERE IdProduct = @IdProduct";
        decimal productPrice = (decimal)command.ExecuteScalar();
        decimal totalPrice = productPrice * request.Amount;

        // Wstawiamy rekord do tabeli Product_Warehouse
        command.CommandText =
            "INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt) " +
            "VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @TotalPrice, @CreatedAt)";
        command.Parameters.AddWithValue("@TotalPrice", totalPrice);
        command.ExecuteNonQuery();

        return Created("", null);
    }
}