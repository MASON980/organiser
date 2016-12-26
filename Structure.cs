//make an sql query for getting the most recent division



//need a Task object


/*
open page brings up all tasks with null/-1 super_task_id

clicking on a task brings up all the tasks with that task's id as super_task_id
*/


/*
use:
	-r:System.Data.dll
	-r:Mono.Data.Sqlite.dll
	
for compiling
*/

using System;
using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;


public class Structure {

	private static string db_name = "organiser.db";
	
	public static SqliteConnection setupDatabase () {
		
		string connectionString = "Data Source="+db_name+";FailIfMissing=false";
		SqliteConnection dbcon = new SqliteConnection(connectionString);

		dbcon.Open();

		string sql_creation = "";

		sql_creation = ""+
			"CREATE TABLE tasks ("+
	//		"ID 			INT PRIMARY KEY,"+
			"task_id        INT     NOT NULL,"+
			"super_task_id	INT,"+
			   
			"title			TEXT,"+
			"blurb			TEXT,"+
			"desc	TEXT,"+
			"related_links	TEXT,"+

			"priority		INT,"+
			"estimate		INT,"+
			"status			INT,"+
			"required		INT,"+
			"due_date		TEXT,"+
			   
			"date			TEXT	NOT NULL"+
		")";
		//   FOREIGN KEY(super_task_id) REFERENCES task(task_id)
		

		
		
				
		IDbCommand db_cmd = dbcon.CreateCommand();

		db_cmd.CommandText = sql_creation;
		db_cmd.ExecuteNonQuery();
		db_cmd.Dispose();
		
		
		sql_creation =
            "INSERT INTO tasks ("+
			"task_id, super_task_id, priority, estimate, status, required, due_date, title, blurb, desc, related_links"+
			") VALUES ("+
			"1, -1, 3, 4, 0, 0, '10/1/2016', 'apple', 'create an apple', 'we are creating an apple, I need to build one', 'none atm'"+
			")";
		db_cmd = dbcon.CreateCommand();

		db_cmd.CommandText = sql_creation;
		db_cmd.ExecuteNonQuery();
		db_cmd.Dispose();
		
		sql_creation =
            "INSERT INTO tasks ("+
			"task_id, super_task_id, priority, estimate, status, required, due_date, title, blurb, desc, related_links"+
			") VALUES ("+
			"2, 1, 2, 2, 1, 0, '2/1/2016', 'build apple', 'build an apple', 'I need to build one', 'none atm'"+
			")";
		db_cmd = dbcon.CreateCommand();

		db_cmd.CommandText = sql_creation;
		db_cmd.ExecuteNonQuery();
		db_cmd.Dispose();
		
	
		return dbcon;
	}
    public static void Main()
    {

       getTasks(-1, "");
    }
	
	public static SqliteConnection connect_db () {
		string connectionString = "Data Source="+db_name+";FailIfMissing=true";
		SqliteConnection dbcon = new SqliteConnection(connectionString);

		try {
			dbcon.Open();
		} catch (Exception e) {
			// the db is empty (I hope)
			dbcon = setupDatabase();
			dbcon.Open();
		}
		return dbcon;
	}
 
	public static List<Task> getTasks (int super_id, string form) {
		// return all tasks with the super_id as the super_task_id
		
		SqliteConnection dbcon = connect_db();
	
		if (form != "") {
			Task t = new Task();
			t.fill_task_with_form(form);
			t.update(dbcon);
		}
		
		IDbCommand dbcmd = dbcon.CreateCommand();

		string sql = "SELECT DISTINCT(task_id) FROM tasks";
		if (super_id >= 0) {
			sql += " WHERE super_task_id="+super_id;
		}
				
		dbcmd.CommandText = sql;
		IDataReader reader = dbcmd.ExecuteReader();

		List<Task> tasks = new List<Task>();

		while(reader.Read())
		{
			tasks.Add(new Task(reader.GetInt32(0), dbcon));

		}
		// clean up
		reader.Dispose();
		dbcmd.Dispose();
		dbcon.Close();

		return tasks;
		//return an array of Tasks
	}

	
}