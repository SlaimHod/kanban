using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Data.SqlClient;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Data.SQLite;

namespace KanbanController
{
	class Task
	{
		public int Id { get; set; }
		public string Text { get; set; }
		public string Priority { get; set; }
		public string Status { get; set; }
	}

	internal class Program
	{
		static void Main(string[] args)
		{
			int startPort = 2000;
			int endPort = 8000; 
			url = $"http://{GetMachineLocalIPAddress()}:2000/";//http://192.168.1.2:{8082}/ /*CheckAvailablePorts(startPort, endPort)*/
			Core();
		}
		static string url = "";
		private static string connectionString = "Data Source=kanban.db;Version = 3;ReadOnly = false"; 

		//получаем локальный IP машины
		static string GetMachineLocalIPAddress()
		{
			try
			{
				var interfaces = NetworkInterface.GetAllNetworkInterfaces()
					.Where(ni =>
						ni.OperationalStatus == OperationalStatus.Up && 
						ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
					.ToList();
				foreach (var ni in interfaces)
				{
					var ipProperties = ni.GetIPProperties();
					var ipv4Address = ipProperties.UnicastAddresses
						.FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);

					if (ipv4Address != null && ipv4Address.Address.ToString().StartsWith("192"))
					{
						return ipv4Address.Address.ToString();
					}
				}

				return "Подходящий IP адрес не найден!";
			}
			catch (Exception ex)
			{
				Console.WriteLine("Ошибка: " + ex.ToString());
				return null;
			}
		}

		static string HandleRequest(HttpListenerRequest request)
		{
			// Определение пути запроса
			string path = request.Url.AbsolutePath;

			if (path == "/")
			{
				return File.ReadAllText("local/kanban.html");
			}

			if (request.HttpMethod == "GET" && path.StartsWith("/api/kanban/tasks"))
			{
				var tasks = GetAllTasks();
				return JsonSerializer.Serialize(tasks);
			}

			if (request.HttpMethod == "POST" && path.StartsWith("/api/kanban/tasks"))
			{
				try
				{
					using (StreamReader reader = new StreamReader(request.InputStream, Encoding.UTF8))
					{
						string requestBody = reader.ReadToEnd();

						var task = JsonSerializer.Deserialize<Task>(requestBody);
						if (task == null)
						{
							return "{\"error\": \"Некорректный формат данных\"}";
						}

						Task addedTask = AddTask(task);
						
						string jsonResponse = JsonSerializer.Serialize(addedTask);
						Console.WriteLine("new " + jsonResponse);
						return jsonResponse;
					}
				}
				catch (Exception ex)
				{

					Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
					return "{\"error\": \"Ошибка на сервере при добавлении задачи\"}";
				}
			}


			if (request.HttpMethod == "PUT" && path.StartsWith("/api/kanban/tasks/"))
			{
				string taskId = path.Split('/').Last();
				using (StreamReader reader = new StreamReader(request.InputStream, Encoding.UTF8))
				{
					string requestBody = reader.ReadToEnd();
					var task = JsonSerializer.Deserialize<Task>(requestBody);
					task.Id = int.Parse(taskId);
					UpdateTask(task);
					return JsonSerializer.Serialize(task);
				}
			}

			if (request.HttpMethod == "DELETE" && path.StartsWith("/api/kanban/tasks/"))
			{
				string taskId = path.Split('/').Last();
				DeleteTask(int.Parse(taskId));
				return "{\"message\": \"Задача удалена\"}";
			}

			return "{\"error\": \"Неизвестный запрос\"}";
		}

		static Task AddTask(Task task)
		{
			using (var connection = new SQLiteConnection(connectionString))
			{
				connection.Open();

				var command = new SQLiteCommand(@"
				INSERT INTO Tasks (Text, Priority, Status)
				VALUES ($text, $priority, $status);
				", connection);
				command.Parameters.AddWithValue("$text", task.Text);
				command.Parameters.AddWithValue("$priority", task.Priority);
				command.Parameters.AddWithValue("$status", task.Status);

				
				var last_row = new SQLiteCommand(@"SELECT last_insert_rowid();", connection);
				//Console.WriteLine(last_row.ExecuteScalar());
				task.Id = Convert.ToInt32(last_row.ExecuteScalar());
			}

			return task;
		}

		static List<Task> GetAllTasks()
		{
			var tasks = new List<Task>();

			using (var connection = new SQLiteConnection(connectionString))
			{
				connection.Open();

				var command = new SQLiteCommand("SELECT * FROM Tasks;", connection);

				using (SQLiteDataReader? reader = command.ExecuteReader())
				{
				
					while (reader.Read())
					{

						tasks.Add(new Task
						{
							Id = reader.GetInt32(0),
							Text = reader.GetString(1),
							Priority = reader.GetString(2),
							Status = reader.GetString(3)
						});

					}
					
				}
			}

			return tasks;
		}
		static void UpdateTask(Task task)
		{
			using (var connection = new SQLiteConnection(connectionString))
			{
				connection.Open();

				var command = new SQLiteCommand(@"
                UPDATE Tasks
                SET Text = $text, Priority = $priority, Status = $status
                WHERE Id = $id;", connection);
				command.Parameters.AddWithValue("$id", task.Id);
				command.Parameters.AddWithValue("$text", task.Text);
				command.Parameters.AddWithValue("$priority", task.Priority);
				command.Parameters.AddWithValue("$status", task.Status);
				command.ExecuteNonQuery();
			}
		}
		static void DeleteTask(int taskId)
		{
			using (var connection = new SQLiteConnection(connectionString))
			{
				connection.Open();

				var command = new SQLiteCommand("DELETE FROM Tasks WHERE Id = $id;", connection);
				command.Parameters.AddWithValue("$id", taskId);
				command.ExecuteNonQuery();
			}
		}
	


	static void Core()
		{
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(url);
			listener.Start();
			Console.WriteLine($"Сервер запущен на {url}");

			while (true)
			{
				HttpListenerContext context = listener.GetContext();
				HttpListenerRequest request = context.Request;
				HttpListenerResponse response = context.Response;
				string path = request.Url.AbsolutePath;

				try
				{
					
					string responseString = HandleRequest(request);
					
					byte[] buffer = Encoding.UTF8.GetBytes(responseString);

					
					if (responseString.StartsWith("<!DOCTYPE html>"))
					{
						response.ContentType = "text/html";
					}
					else
					{
						Console.WriteLine(responseString);
						response.ContentType = "application/json";
					}
					response.ContentLength64 = buffer.Length;
					response.OutputStream.Write(buffer, 0, buffer.Length);
				}
				catch (Exception ex)
				{
					/*string errorResponse = $"{{\"error\": \"{ex.Message}\"}}";
					byte[] buffer = Encoding.UTF8.GetBytes(errorResponse);
					response.StatusCode = (int)HttpStatusCode.InternalServerError;
					response.ContentType = "application/json";
					response.ContentLength64 = buffer.Length;
					response.OutputStream.Write(buffer, 0, buffer.Length);*/
				}
				finally
				{
					response.OutputStream.Close();
				}
			}
		}
	}
}