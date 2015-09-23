using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration.Provider;
using System.Linq;
using System.Web.Hosting;
using Alienlab.Web.Security.Resources;
using Alienlab.Web.Security.Store;

namespace Alienlab.Web.Security {

    /// <summary>
    /// Custom XML implementation of <c>System.Web.Security.RoleProvider</c>
    /// </summary>
    public class XmlRoleProvider : RoleProviderBase, IDisposable {

        #region Fields  ///////////////////////////////////////////////////////////////////////////

        string _file;
        private XmlRoleStore _store;

        #endregion

        #region Properties  ///////////////////////////////////////////////////////////////////////


        /// <summary>
        /// Gets the roles.
        /// </summary>
        /// <value>The roles.</value>
        protected List<XmlRole> Roles { get { return this.Store.Roles; } }

        /// <summary>
        /// Gets the role store.
        /// </summary>
        /// <value>The role store.</value>
        protected XmlRoleStore Store {
            get {
                return _store ?? (_store = new XmlRoleStore(_file));
            }
        }


        #endregion

        #region Construct / Destruct //////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlRoleProvider"/> class.
        /// </summary>
        public XmlRoleProvider() {
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            Store.Dispose();
        }


        #endregion

        #region Methods ///////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified user names to the specified roles for the configured applicationName.
        /// </summary>
        /// <param name="usernames">A string array of user names to be added to the specified roles.</param>
        /// <param name="roleNames">A string array of the role names to add the specified user names to.</param>
        public override void AddUsersToRoles(string[] usernames, string[] roleNames) {

            if (usernames == null)
                throw new ArgumentNullException("usernames");
            if (roleNames == null)
                throw new ArgumentNullException("roleNames");

            var comparer = this.Comparer;
            lock (SyncRoot) {
                foreach (string rolename in roleNames) {
                    XmlRole role = this.GetRole(rolename);
                    if (role != null) {
                        foreach (string username in usernames) {
                            if (!role.Users.Contains(username, comparer))
                                role.Users.Add(username);
                        }
                    }
                }
                this.Store.Save();
            }
        }

        /// <summary>
        /// Adds a new role to the data source for the configured applicationName.
        /// </summary>
        /// <param name="roleName">The name of the role to create.</param>
        public override void CreateRole(string roleName) {

            if (roleName == null)
                throw new ArgumentNullException("roleName");
            if (roleName.IndexOf(',') > 0)
                throw new ArgumentException(Messages.RoleCannotContainCommas);

            XmlRole role = this.GetRole(roleName);
            if (role == null) {
                role = new XmlRole {
                    Name = roleName,
                    Users = new List<string>()
                };
                lock (SyncRoot) {
                    this.Store.Roles.Add(role);
                    this.Store.Save();
                }
            }
            else
                throw new ProviderException(Messages.RoleExists.F(roleName));
        }

        /// <summary>
        /// Removes a role from the data source for the configured applicationName.
        /// </summary>
        /// <param name="roleName">The name of the role to delete.</param>
        /// <param name="throwOnPopulatedRole">If true, throw an exception if roleName has one or more members and do not delete roleName.</param>
        /// <returns>
        /// true if the role was successfully deleted; otherwise, false.
        /// </returns>
        public override bool DeleteRole(string roleName, bool throwOnPopulatedRole) {

            if (roleName == null)
                throw new ArgumentNullException("roleName");

            lock (SyncRoot) {
                XmlRole role = this.GetRole(roleName);
                if (role != null) {
                    if (throwOnPopulatedRole && (role.Users.Count > 0))
                        throw new ProviderException(Messages.CannotDeletePopulatedRole);
                    this.Store.Roles.Remove(role);
                    this.Store.Save();

                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets an array of user names in a role where the user name contains the specified user name to match.
        /// </summary>
        /// <param name="roleName">The role to search in.</param>
        /// <param name="usernameToMatch">The user name to search for.</param>
        /// <returns>
        /// A string array containing the names of all the users where the user name matches usernameToMatch and the user is a member of the specified role.
        /// </returns>
        public override string[] FindUsersInRole(string roleName, string usernameToMatch) {

            if (roleName == null)
                throw new ArgumentNullException("roleName");
            if (usernameToMatch == null)
                throw new ArgumentNullException("usernameToMatch");

            var comparison = this.Comparison;
            var query = from role in this.Roles.AsQueryable()
                        from user in role.Users
                        where (user.IndexOf(usernameToMatch, comparison) >= 0)
                                && role.Name.Equals(roleName, comparison)
                        select user;
            lock (SyncRoot) {
                return query.ToArray();
            }
        }

        /// <summary>
        /// Gets a list of all the roles for the configured applicationName.
        /// </summary>
        /// <returns>
        /// A string array containing the names of all the roles stored in the data source for the configured applicationName.
        /// </returns>
        public override string[] GetAllRoles() {
            var query = from r in this.Roles
                        select r.Name;
            lock (SyncRoot) {
                return query.ToArray();
            }
        }

        /// <summary>
        /// Gets the role.
        /// </summary>
        /// <param name="roleName">The name.</param>
        /// <returns></returns>
        public XmlRole GetRole(string roleName) {

            if (roleName == null)
                throw new ArgumentNullException("roleName");

            var query = from r in this.Roles
                        where r.Name.Equals(roleName, this.Comparison)
                        select r;
            lock (SyncRoot) {
                return query.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets a list of the roles that a specified user is in for the configured applicationName.
        /// </summary>
        /// <param name="username">The user to return a list of roles for.</param>
        /// <returns>
        /// A string array containing the names of all the roles that the specified user is in for the configured applicationName.
        /// </returns>
        public override string[] GetRolesForUser(string username) {

            if (username == null)
                throw new ArgumentNullException("username");
            var query = from r in this.Roles
                        where r.Users.Contains(username, this.Comparer)
                        select r.Name;
            lock (SyncRoot) {
                return query.ToArray();
            }
        }

        /// <summary>
        /// Gets a list of users in the specified role for the configured applicationName.
        /// </summary>
        /// <param name="roleName">The name of the role to get the list of users for.</param>
        /// <returns>
        /// A string array containing the names of all the users who are members of the specified role for the configured applicationName.
        /// </returns>
        public override string[] GetUsersInRole(string roleName) {

            XmlRole role = this.GetRole(roleName);
            if (role != null) {
                lock (SyncRoot) {
                    return role.Users.ToArray();
                }
            }
            throw new ProviderException(Messages.RoleNotExists.F(roleName));
        }

        /// <summary>
        /// Gets a value indicating whether the specified user is in the specified role for the configured applicationName.
        /// </summary>
        /// <param name="username">The user name to search for.</param>
        /// <param name="roleName">The role to search in.</param>
        /// <returns>
        /// true if the specified user is in the specified role for the configured applicationName; otherwise, false.
        /// </returns>
        public override bool IsUserInRole(string username, string roleName) {

            if (username == null)
                throw new ArgumentNullException("username");

            XmlRole role = this.GetRole(roleName);
            if (role != null) {
                lock (SyncRoot) {
                    return role.Users.Contains(username, this.Comparer);
                }
            }
            else
                throw new ProviderException(Messages.RoleNotExists.F(roleName));
        }

        /// <summary>
        /// Removes the specified user names from the specified roles for the configured applicationName.
        /// </summary>
        /// <param name="usernames">A string array of user names to be removed from the specified roles.</param>
        /// <param name="roleNames">A string array of role names to remove the specified user names from.</param>
        public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames) {

            if (usernames == null)
                throw new ArgumentNullException("usernames");
            if (roleNames == null)
                throw new ArgumentNullException("roleNames");

            var comparer = this.Comparer;
            var query = from r in this.Roles
                        where roleNames.Contains(r.Name, comparer)
                        select r;
            lock (SyncRoot) {
                foreach (XmlRole role in query) {
                    foreach (string username in usernames) {
                        role.Users.Remove(username);
                    }
                }
                this.Store.Save();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the specified role name already exists in the role data source for the configured applicationName.
        /// </summary>
        /// <param name="roleName">The name of the role to search for in the data source.</param>
        /// <returns>
        /// true if the role name already exists in the data source for the configured applicationName; otherwise, false.
        /// </returns>
        public override bool RoleExists(string roleName) {
            return this.GetRole(roleName) != null;
        }

        #region - Initialize -

        /// <summary>
        /// Initializes the provider.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing the provider-specific attributes specified in the configuration for this provider.</param>
        /// <exception cref="T:System.ArgumentNullException">The name of the provider is null.</exception>
        /// <exception cref="T:System.InvalidOperationException">An attempt is made to call <see cref="M:System.Configuration.Provider.ProviderBase.Initialize(System.String,System.Collections.Specialized.NameValueCollection)"></see> on a provider after the provider has already been initialized.</exception>
        /// <exception cref="T:System.ArgumentException">The name of the provider has a length of zero.</exception>
        public override void Initialize(string name, NameValueCollection config) {

            if (config == null)
                throw new ArgumentNullException("config");

            // prerequisite
            if (string.IsNullOrWhiteSpace(name)) {
                name = "XmlRoleProvider";
            }
            if (string.IsNullOrEmpty(config["description"])) {
                config.Remove("description");
                config.Add("description", "XML Role Provider");
            }

            // initialize the base class
            base.Initialize(name, config);

            // initialize provider fields
            string fileName = config.GetString("fileName", "Roles.xml");
            string folder = config.GetString("folder", "~/App_Data/");

            if (!folder.EndsWith("/")) folder += "/";
            _file = HostingEnvironment.MapPath(string.Format("{0}{1}", folder, fileName));
        }
        #endregion
        #endregion
    }
}