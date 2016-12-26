// task class for organiser

using System;
using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;
 
public class Task {

	private static string[] cultureNames = { "en-US", "en-GB", "fr-FR", "de-DE", "ru-RU" };

	private static IFormatProvider culture = new System.Globalization.CultureInfo(cultureNames[1], true);	

	private static string[] STATUSES = {"planning", "opened", "finalising", "closed"};
	private static string[] REQUIREDS = {"yes", "no", "unknown"};
	
	/*
		when a task is updated it could either store the entire thing, or only the new stuff (everything else being null), it would be a matter of storage/processing
		
		storing the entire things would be somewhat easier and it should be easy to change regardless
			however you wouldn't know when a specific value was last updated without running through everything and comparing
	*/
	
	//private static enum priorities { required, optional};
	public enum statuses { planning, ongoing, testing, finalising, completed, archived, backburner, abandoned, unknown, inviable };
	
	// https://stackoverflow.com/questions/2952192/how-can-i-create-an-enum-using-numbers
	//private static enum priority { 0, 1, 2, 3, 4, 5};
	// probably shouldn't be an enum
	
	
	private int task_id;
	private Nullable<int> super_task_id = null;
	private int direct_subtasks = 0;
	private int all_subtasks = 0;
	
	private Nullable<int> priority = null;
	private Nullable<int> estimate = null;
	private Nullable<int> status = null;
	private Nullable<int> required = null;
	private Nullable<DateTime> due_date = null;

	private string title = null;
	private string blurb = null;
	private string desc = null;
	private string related_links = null;
	private List<string> tags = null;
	
	private Nullable<DateTime> last_updated;
	private Nullable<DateTime> first_updated;
	
	public Task () {
		// set up an empty task
		
		// I think all these things should be null, rather than 0 and so, so the sql generation knows whether it should be input or not because both new tasks and insert tasks are using this
		this.task_id = -1;
		
		this.super_task_id = null;
		
		this.priority = null;
		this.estimate = null;
		this.status = null;
		this.required = null;
		this.due_date = null;
		
		this.title = null;
		this.blurb = null;
		this.desc = null;
		this.related_links = null;
		
		this.last_updated = DateTime.Now;
		this.first_updated = DateTime.Now;
		
	}
	public bool fill_task_with_form (string form) {
		string[] parameters = form.Split('&');
		
		foreach (string pr in parameters) {
			string[] ps = pr.Split('=');
			input_value(ps[0], ps[1]);
		}
		return true;
	}
	
	public Task (int task_id, SqliteConnection db) {
		// read a task in from the database

		SqliteCommand command = db.CreateCommand();
		string sql = "SELECT * FROM tasks WHERE task_id=@id ORDER BY date DESC";
				
		command.CommandText = sql;
		command.Parameters.Add(new SqliteParameter("@id", task_id));

		IDataReader reader = command.ExecuteReader();
		
		
		int needed = 11;	// number of rows needed
		 
		while(reader.Read())
		{
			int found = takeValues(reader);
			needed -= found;
			if (needed <= 0) {
				break;
			}
		}
		// clean up
		reader.Dispose();
		

		int[] sub_ret = find_subtasks(false, 0, task_id, db);
		this.direct_subtasks = sub_ret[0];
		
		sub_ret = find_subtasks(true, 0, task_id, db);
		this.all_subtasks = sub_ret[0];
		
		//this.all_subtasks = total_found;
		command.Dispose();
	
		this.task_id = task_id;

	}
	
	public int[] find_subtasks (bool recursion, int level, int super_id, SqliteConnection db) {
		int[] open_ret = {0, 0};
		if (level > 10) return open_ret;
		
		SqliteCommand command = db.CreateCommand();
		string sql = "SELECT task_id, estimate, super_task_id FROM tasks WHERE task_id in (SELECT distinct(task_id) from tasks WHERE super_task_id=@id) ORDER BY date DESC";
		command.CommandText = sql;
		command.Parameters.Add(new SqliteParameter("@id", super_id));

		IDataReader reader = command.ExecuteReader();
		
		
		int found = 0;
		int found_estimate = 0;
		List<int> check = new List<int>();
		while(reader.Read())
		{
			bool already = false;
			foreach (int ch in check) {
				if (ch == reader.GetInt32(0)) {
					already = true;
					break;
				}
			}
			if (!already && reader.GetInt32(2) == task_id) {
				found++;
				found_estimate += reader.GetInt32(1);
				if (recursion) {
					int[] ret = find_subtasks (recursion, level+1, reader.GetInt32(0), db);
					found += ret[0];
					found_estimate += ret[1];
				}
			}
			check.Add(reader.GetInt32(0));
		}
		int[] full_ret = {found, found_estimate};
		return full_ret;
	}

	public bool update (SqliteConnection db) {
		// get the current databases task
		bool ret = false;
		
		if (this.task_id != -1) {
			Task og_task = new Task(this.task_id, db);
			Task insert_task = new Task();
			
			// compare each value and for every new one give it to insert_task
			insert_task = insertDifferents(insert_task, this, og_task);
			
			// dont update the db with a thing which doesn't have any changes
			if (insert_task == null) return false;
			
			insert_task.task_id = this.task_id;
			
			ret = insert_task.submitSQL(db);
			this.last_updated = insert_task.last_updated;

		} else {
			ret = this.submitSQL(db);
		}
		
		return ret;
	}

	public bool submitSQL (SqliteConnection db) {
		// insert this task into a database
		SqliteCommand query = this.ToSQL(db);
		try {
			query.ExecuteNonQuery();
		    query.Dispose();
		} catch (Exception e) {
		    query.Dispose();
			return false;
		}

		return true;
	}
	
	public SqliteCommand ToSQL (SqliteConnection db) {
		// generate an sanitized sql query with all the non-null values in the thing
		
		this.last_updated = DateTime.Now;
		
		SqliteCommand  command = db.CreateCommand();
        command.CommandText =
            "INSERT INTO tasks ("+
			"task_id, super_task_id, priority, estimate, status, required, due_date, title, blurb, desc, related_links, date"+
			") VALUES ("+
			"@task_id, @super_task_id, @priority, @estimate, @status, @required, @due_date, @title, @blurb, @desc, @related_links, @date"+
			")";

		int task_id = this.task_id;
		if (task_id == -1) {
			task_id = findNextTaskId(db);
		}

		command.Parameters.Add(new SqliteParameter("@task_id", task_id));
		
		SqliteParameter[] parameters = { 
		
			new SqliteParameter("@super_task_id", this.super_task_id),
			
			new SqliteParameter("@priority", this.priority),
			new SqliteParameter("@estimate", this.estimate),
			new SqliteParameter("@status", this.status),
			new SqliteParameter("@required", this.required),
			new SqliteParameter("@due_date", this.due_date),
			
			new SqliteParameter("@title", this.title),
			new SqliteParameter("@blurb", this.blurb),
			new SqliteParameter("@desc", this.desc),
			new SqliteParameter("@related_links", this.related_links),

			new SqliteParameter("@tags", "'"+String.Join(", ", this.tags.ToArray())+"'"),

			new SqliteParameter("@date", this.last_updated),
			
		};
		command.Parameters.AddRange(parameters);
        // Call  Prepare and ExecuteNonQuery.
        command.Prepare();
		
		//command.ExecuteNonQuery();

		return command;
	}
	public string ToJSON () {
		/*
		var task_object = {
			
			"title":"",
			"blurb":"",
			"desc":"",
			
			"estimate":1,
			"priority":1,
			"required":0,
			"status":0,
			"due_date":"",	// get current date
			"super_task_id":-1,		
			
		};
		*/
		string status_name = "";
		if (this.status.HasValue) status_name = STATUSES[this.status.Value];
		string required_name = "";
		if (this.required.HasValue) required_name = REQUIREDS[this.required.Value];
		
		string task_object = "";
		
		//task_object += this.task_id.ToString()+":";
		
		task_object += "{";
		task_object += "\"task_id\":\""+this.task_id.ToString()+"\",";
		task_object += "\"title\":\""+this.title+"\",";
		task_object += "\"blurb\":\""+this.blurb+"\",";
		task_object += "\"desc\":\""+this.desc+"\",";
		task_object += "\"related_links\":\""+this.related_links+"\",";
		
		task_object += "\"tags\":[\""+String.Join("\", \"", this.tags.ToArray())+"\"],";
		
		if (this.super_task_id.HasValue) task_object += "\"super_task_id\":\""+this.super_task_id.Value.ToString()+"\",";

		if (this.priority.HasValue) task_object += "\"priority\":\""+this.priority.Value.ToString()+"\",";
		if (this.estimate.HasValue) task_object += "\"estimate\":\""+this.estimate.Value.ToString()+"\",";
		if (this.status.HasValue) task_object += "\"status\":\""+status_name+"\",";
		if (this.required.HasValue) task_object += "\"required\":\""+required_name+"\",";
		if (this.due_date.HasValue) task_object += "\"due_date\":\""+this.due_date.Value.ToString()+"\",";

		task_object += "\"direct_subtasks\":\""+this.direct_subtasks.ToString()+"\",";
		//task_object += "\"direct_subtasks_estimate\":\""+this.direct_subtasks_estimate.ToString()+"\",";
		task_object += "\"all_subtasks\":\""+this.all_subtasks.ToString()+"\",";
		//task_object += "\"all_subtasks_estimate\":\""+this.all_subtasks_estimate.ToString()+"\",";

		if (this.first_updated.HasValue) task_object += "\"first_updated\":\""+this.first_updated.Value.ToString()+"\",";
		if (this.last_updated.HasValue) task_object += "\"last_updated\":\""+this.last_updated.Value.ToString()+"\",";
		
		
		task_object += "\"padding\":\"\"";

		task_object += "}";
		return task_object;
	}
	
	public string ToHTML_list () {
		// html which is shown when the task is being viewed in a list
		string task_string = "";
		
		
		string status_name = STATUSES[this.status.Value];
		//string required_name = REQUIREDS[this.required.Value];
		
		task_string += "<div id='task_"+this.task_id+"' class='task status_"+status_name+"_task'> ";
		
		task_string += "<b>"+this.title+"</b> ";
		task_string += "<span class='status'>"+status_name+"</span> ";
		task_string += " "+this.estimate.Value+" ";
		
		//task_string += ""+this.blurb+"";
		task_string += " <button class='add_task' onclick='setup_add_form(\""+this.task_id+"\")'>Add</button> ";
		task_string += " <button class='edit_task' onclick='setup_edit_form(\""+this.task_id+"\")'>Edit</button> ";
		task_string += " <button class='open_task' onclick='view_task(\""+this.task_id+"\")'>View</button> ";
				
		task_string += " <div class='sub_tasks' id='task_"+this.task_id+"_subtasks_wrapper' ><asp:label runat='server' id='asp_task_"+this.task_id+"' /></div> ";
		task_string += " <div class='task_form' id='task_"+this.task_id+"_form_wrapper' ></div> ";
		
		task_string += "<script id='task_"+this.task_id+"_json'>"+this.ToJSON()+"</script>";
		task_string += "</div>";
		return task_string;
	}
	
	public string ToHTML () {
		// generate html based upon the values this task has
		return ToHTML_list();
		/*
		string task_string = "";
		
		this.super_task_id = -1;
		
		this.priority = 0;
		this.estimate = 0;
		this.status = 0;
		this.required = 0;
		this.due_date = default(DateTime);
		
		this.title = "";
		this.blurb = "";
		this.desc = "";		//	anything with 'NOTE:', 'WARNING:', 'TODO:' is highlighted in some way

		this.related_links = "";
		
		this.last_updated = DateTime.Now;
		this.first_updated = DateTime.Now;
		
		task_string = "<div>apple <b>bnana</b></div>";

		
		task_string = "<div class='task'><b></b></div>";

				
		return task_string;*/
	}

	
	
	
	
		// this is more of a database thing than a task thing
	private static int findNextTaskId (SqliteConnection db) {
		
		SqliteCommand command = db.CreateCommand();
        command.CommandText =
            "SELECT task_id FROM tasks ORDER BY task_id DESC LIMIT 1";
			
		command.Prepare();
		
		IDataReader reader = command.ExecuteReader();
		 
		int task_id = 0;
		while(reader.Read())
		{
			task_id = reader.GetInt32(0)+1;
		}
		reader.Dispose();
		command.Dispose();

		return task_id;
	}
	
	
	// mostly boring getter/setter-esque stuff
	
	
	private int takeValues (IDataReader reader) {
		int found = 0;

		if (this.title == null) this.title = getString(reader, "title");		
		if (this.title != null) found++;

		if (this.blurb == null) this.blurb = getString(reader, "blurb");		
		if (this.blurb != null) found++;
	
		if (this.desc == null) this.desc = getString(reader, "desc");		
		if (this.desc != null) found++;
		
		if (this.related_links == null) this.related_links = getString(reader, "related_links");		
		if (this.related_links != null) found++;
				
		if (this.tags == null) this.tags = getListString(reader, "tags");		
		if (this.tags != null) found++;
				

		if (!this.super_task_id.HasValue) this.super_task_id = getInt(reader, "super_task_id");		
		if (this.super_task_id.HasValue) found++;

		if (!this.priority.HasValue) this.priority = getInt(reader, "priority");		
		if (this.priority.HasValue) found++;
		
		if (!this.estimate.HasValue) this.estimate = getInt(reader, "estimate");		
		if (this.estimate.HasValue) found++;
		
		if (!this.status.HasValue) this.status = getInt(reader, "status");		
		if (this.status.HasValue) found++;

		if (!this.required.HasValue) this.required = getInt(reader, "required");		
		if (this.required.HasValue) found++;

		if (!this.due_date.HasValue) this.due_date = getDate(reader, "due_date");		
		if (this.due_date.HasValue) found++;

		if (!this.last_updated.HasValue) this.last_updated = getDate(reader, "last_updated");		
		if (this.last_updated.HasValue) found++;

		return found;
	}
	private Nullable<int> getInt (IDataReader reader, string key) {
		Nullable<int> val = null;
		if (!isDBNull(reader, key)) {
			val = Int32.Parse(reader[key].ToString());
		}		
		return val;
	}
	private Nullable<DateTime> getDate (IDataReader reader, string key) {
		Nullable<DateTime> val = null;
		if (!isDBNull(reader, key)) {
			string date = reader[key].ToString();
			
			try {
				val = DateTime.Parse(date);//, culture);
			} catch (Exception e) {
				// invalid datetime, maybe, shouldn't occur in proper thing
				Console.Write("string not valid datetime");
				val = null;
			}			
		}	
		return val;
	}
	private string getString (IDataReader reader, string key) {
		string val = null;
		if (!isDBNull(reader, key)) {
			val = reader[key].ToString();
		}
		return val;
	}
	private List<string> getListString (IDataReader reader, string key) {
		string val = getString(reader, key);
		List<string> li = val.Split(", ").ToList();
		return li;
	}
	private bool isDBNull (IDataReader reader, string key) {
		int index = reader.GetOrdinal(key);		
		return reader.IsDBNull(index);
	}
	
	private void input_value (string key, string v) {
		
		if (key == "title") this.title = v;
		if (key == "blurb") this.blurb = v;
		if (key == "desc") this.desc = v;
		if (key == "related_links") this.related_links = v;
		if (key == "tags") this.tags = v.Split(", ").ToList();
		
		if (key == "super_task_id") this.super_task_id = Int32.Parse(v);
		if (key == "task_id") this.task_id = Int32.Parse(v);

		if (key == "priority") this.priority = Int32.Parse(v);
		if (key == "estimate") this.estimate = Int32.Parse(v);
		if (key == "status") {
			int nv = Array.IndexOf(STATUSES, v);
			this.status = nv;
		}
		if (key == "required") {
			int nv = Array.IndexOf(REQUIREDS, v);
			this.required = nv;
		}
		
		if (key == "due_date") this.due_date = DateTime.Parse(v);//, culture);	//, System.Globalization.DateTimeStyles.AssumeLocal
			
	}
	
	private static Task insertDifferents (Task nt, Task ct, Task ot) {
		// put ct into nt if ct is different from ot
		bool changed = false;
		
		if (ct.title != ot.title) {
			nt.title = ct.title;
			changed = true;
		}
		if (ct.blurb != ot.blurb) {
			nt.blurb = ct.blurb;
			changed = true;
		}
		if (ct.desc != ot.desc) {
			nt.desc = ct.desc;
			changed = true;
		}
		if (ct.related_links != ot.related_links) {
			nt.related_links = ct.related_links;
			changed = true;
		}		

		if (String.Join(" ", ct.tags.ToArray()) != String.Join(" ", ot.tags.ToArray())) {
			nt.tags = ct.tags;
			changed = true;			
		}
		
		
		if (ct.super_task_id != ot.super_task_id) {
			nt.super_task_id = ct.super_task_id.Value;
		}
		
		if (ct.priority != ot.priority) {
			nt.priority = ct.priority.Value;
			changed = true;
		}
		if (ct.estimate != ot.estimate) {
			nt.estimate = ct.estimate.Value;
			changed = true;
		}
		if (ct.status != ot.status) {
			nt.status = ct.status.Value;
			changed = true;
		}
		if (ct.required != ot.required) {
			nt.required = ct.required.Value;
			changed = true;
		}		

		if (ct.due_date != ot.due_date) {
			nt.due_date = ct.due_date;
			changed = true;
		}
		
		if (changed == false) return null;
		return nt;
	}
	
}