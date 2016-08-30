using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using YummyOnlineDAO.Models;

namespace YummyOnlineDAO.Identity {
	public abstract class BaseUserManager {
		public BaseUserManager() {
			ctx = new YummyOnlineContext();
		}

		protected YummyOnlineContext ctx;

		public string GetMd5(string str) {
			string pwd = "";
#if DEBUG
			pwd = str;
#else
			MD5 md5 = MD5.Create();
			byte[] s = md5.ComputeHash(Encoding.UTF8.GetBytes(str));
			for(int i = 0; i < s.Length; i++) {
				pwd = pwd + s[i].ToString("X");
			}
#endif
			return pwd;
		}
	}


	public class UserManager : BaseUserManager {
		public UserManager() {
			ctx = new YummyOnlineContext();
		}

		public async Task<bool> IsPhoneNumberDuplicated(string phone) {
			return await ctx.Users.FirstOrDefaultAsync(p => p.PhoneNumber == phone) != null;
		}

		private async Task<bool> createAsync(User user) {
			ctx.Users.Add(user);
			try {
				await ctx.SaveChangesAsync();
				return true;
			}
			catch(Exception e) {
				await new YummyOnlineManager().RecordLog(Log.LogProgram.Identity, Log.LogLevel.Error, e.Message, e.ToString());
				return false;
			}
		}
		public async Task<bool> CreateAsync(User user, string password) {
			user.PasswordHash = GetMd5(password);
			return await createAsync(user);
		}

		public async Task<User> CreateVoidUserAsync() {
			User voidUser = new User();
			bool succeeded = await createAsync(voidUser);
			if(!succeeded) {
				return null;
			}
			return voidUser;
		}
		public async Task<bool> DeleteAsync(User user) {
			ctx.Users.Remove(user);
			try {
				await ctx.SaveChangesAsync();
				return true;
			}
			catch(Exception e) {
				await new YummyOnlineManager().RecordLog(Log.LogProgram.Identity, Log.LogLevel.Error, e.Message, e.ToString());
				return false;
			}
		}
		public async Task<bool> UpdateAsync(User user) {
			ctx.Entry(user).State = EntityState.Modified;
			try {
				await ctx.SaveChangesAsync();
				return true;
			}
			catch(Exception e) {
				await new YummyOnlineManager().RecordLog(Log.LogProgram.Identity, Log.LogLevel.Error, e.Message, e.ToString());
				return false;
			}
		}

		public async Task<List<Role>> GetRolesAsync(string userId) {
			User user = await FindByIdAsync(userId);
			List<Role> roles = await ctx.UserRoles.Where(p => p.User.Id == userId).Select(p => p.Role).ToListAsync();
			return roles;
		}
		public async Task AddToRoleAsync(string userId, Role role) {
			ctx.UserRoles.Add(new UserRole {
				UserId = userId,
				Role = role
			});
			await ctx.SaveChangesAsync();
		}
		public async Task RemoveFromRoleAsync(string userId, Role role) {
			UserRole userRole = await ctx.UserRoles.Where(p => p.UserId == userId && p.Role == role).FirstOrDefaultAsync();
			ctx.UserRoles.Remove(userRole);
			await ctx.SaveChangesAsync();
		}
		public async Task<bool> IsInRoleAsync(string userId, Role role) {
			UserRole userRole = await ctx.UserRoles.Where(p => p.UserId == userId && p.Role == role).FirstOrDefaultAsync();
			if(userRole == null) {
				return false;
			}
			return true;
		}

		public async Task<User> FindByIdAsync(string id) {
			return await ctx.Users.FirstOrDefaultAsync(p => p.Id == id);
		}
		public async Task<User> FindByPhoneNumberAsync(string phone) {
			return await ctx.Users.FirstOrDefaultAsync(p => p.PhoneNumber == phone);
		}
		public async Task<User> FindByEmailAsync(string email) {
			return await ctx.Users.FirstOrDefaultAsync(p => p.Email == email);
		}
		public async Task<bool> CheckPasswordAsync(User user, string password) {
			User realUser = await FindByIdAsync(user.Id);
			if(realUser.PasswordHash != GetMd5(password)) {
				return false;
			}
			return true;
		}
		public async Task ChangePasswordAsync(string phoneNumber, string newPassword) {
			User user = await FindByPhoneNumberAsync(phoneNumber);
			user.PasswordHash = GetMd5(newPassword);
			ctx.Entry(user).Property(p => p.PasswordHash).IsModified = true;
			await ctx.SaveChangesAsync();
		}

		public async Task TransferUserPrice(User newUser, User oldUser) {
			newUser.Price += oldUser.Price;
			oldUser.Price = 0;
			await ctx.SaveChangesAsync();
		}
	}
}
